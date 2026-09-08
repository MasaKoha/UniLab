#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MessagePack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace UniLab.MasterData.Editor.Validation
{
    /// <summary>JSON 契約と MessagePack 対象メンバーの共通部分を双方向に照合する。</summary>
    public sealed class MasterSchemaValidator : IMasterValidator
    {
        private readonly DefaultContractResolver _resolver = new DefaultContractResolver();
        private readonly Func<Type, string, bool> _isSourceExcluded;
        private readonly Func<MasterValidationContext, string, bool> _isNullAllowed;

        /// <summary>除外は宣言型とメンバー名、null 許可は入力位置で明示指定する。</summary>
        public MasterSchemaValidator(Func<Type, string, bool> isSourceExcluded,
            Func<MasterValidationContext, string, bool> isNullAllowed)
        {
            _isSourceExcluded = isSourceExcluded;
            _isNullAllowed = isNullAllowed;
        }

        /// <summary>元文字列の重複と、パース済み入力の契約違反を検出する。</summary>
        public void Validate(MasterValidationContext context, MasterValidationResult result)
        {
            ValidateDuplicateKeys(context, result);
            ValidateToken(context.Root, context.MasterType, "$", context, result);
        }

        private void ValidateToken(JToken token, Type serializedType, string path,
            MasterValidationContext context, MasterValidationResult result)
        {
            if (token.Type == JTokenType.Null)
            {
                if (!_isNullAllowed(context, path) || (serializedType.IsValueType && Nullable.GetUnderlyingType(serializedType) == null))
                {
                    Add(context, result, "Null", path, "明示 null は許可されていません。", serializedType.Name, "null");
                }
                return;
            }

            var contract = _resolver.ResolveContract(Nullable.GetUnderlyingType(serializedType) ?? serializedType);
            if (contract is JsonArrayContract arrayContract && token is JArray array)
            {
                for (var index = 0; index < array.Count; index++)
                {
                    ValidateToken(array[index], arrayContract.CollectionItemType!, $"{path}[{index}]", context, result);
                }
                return;
            }
            if (contract is JsonObjectContract objectContract && token is JObject jsonObject)
            {
                ValidateObject(jsonObject, objectContract, path, context, result);
                return;
            }
            ValidateScalar(token, serializedType, path, context, result);
        }

        private void ValidateObject(JObject jsonObject, JsonObjectContract contract, string path,
            MasterValidationContext context, MasterValidationResult result)
        {
            var members = contract.Properties.Where(IsSourceMember).ToDictionary(property => property.PropertyName!, StringComparer.Ordinal);
            foreach (var property in jsonObject.Properties())
            {
                if (!members.ContainsKey(property.Name))
                {
                    Add(context, result, "ExtraKey", path + "." + property.Name,
                        "JSON キーに対応するソース入力メンバーがありません。", "登録済みメンバー", property.Name);
                }
            }
            foreach (var member in members.Values)
            {
                var memberPath = path + "." + member.PropertyName;
                if (!jsonObject.TryGetValue(member.PropertyName!, StringComparison.Ordinal, out var value))
                {
                    Add(context, result, "MissingKey", memberPath, "必須キーがありません。", member.PropertyName!, "欠落");
                    continue;
                }
                ValidateToken(value, member.PropertyType!, memberPath, context, result);
            }
        }

        private bool IsSourceMember(JsonProperty property)
        {
            if (property.Ignored || _isSourceExcluded(property.DeclaringType!, property.UnderlyingName!))
            {
                return false;
            }
            // Key の値は MessagePack 専用の別名なので JSON 名の決定には使わない。
            return property.AttributeProvider!.GetAttributes(typeof(KeyAttribute), true).Count > 0
                && property.AttributeProvider.GetAttributes(typeof(IgnoreMemberAttribute), true).Count == 0;
        }

        private void ValidateScalar(JToken token, Type serializedType, string path,
            MasterValidationContext context, MasterValidationResult result)
        {
            var valueType = Nullable.GetUnderlyingType(serializedType) ?? serializedType;
            var expectedToken = ResolveScalarToken(valueType);
            var matches = token.Type == expectedToken || (expectedToken == JTokenType.Float && token.Type == JTokenType.Integer);
            if (!matches || expectedToken == JTokenType.None)
            {
                Add(context, result, "Type", path, "JSON の値の型が一致しません。", serializedType.Name, token.Type.ToString());
                return;
            }
            try
            {
                var restored = token.ToObject(serializedType);
                if ((restored is float single && (float.IsInfinity(single) || float.IsNaN(single)))
                    || (restored is double number && (double.IsInfinity(number) || double.IsNaN(number))))
                {
                    Add(context, result, "Type", path, "有限数として復元できません。", serializedType.Name, token.ToString(Formatting.None));
                }
            }
            catch (Exception exception) when (exception is JsonException || exception is OverflowException || exception is FormatException || exception is ArgumentException)
            {
                Add(context, result, "Type", path, "値を対象型へ復元できません。", serializedType.Name, token.ToString(Formatting.None));
            }
        }

        private static JTokenType ResolveScalarToken(Type valueType)
        {
            if (valueType == typeof(string)) { return JTokenType.String; }
            if (valueType == typeof(bool)) { return JTokenType.Boolean; }
            if (valueType.IsEnum) { return JTokenType.Integer; }
            switch (Type.GetTypeCode(valueType))
            {
                case TypeCode.Byte: case TypeCode.SByte: case TypeCode.Int16: case TypeCode.UInt16:
                case TypeCode.Int32: case TypeCode.UInt32: case TypeCode.Int64: case TypeCode.UInt64:
                    return JTokenType.Integer;
                case TypeCode.Single: case TypeCode.Double: case TypeCode.Decimal:
                    return JTokenType.Float;
                default:
                    return JTokenType.None;
            }
        }

        private static void ValidateDuplicateKeys(MasterValidationContext context, MasterValidationResult result)
        {
            using var textReader = new StringReader(context.RawJson);
            using var reader = new JsonTextReader(textReader) { DateParseHandling = DateParseHandling.None };
            var objects = new Stack<HashSet<string>>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.StartObject) { objects.Push(new HashSet<string>(StringComparer.Ordinal)); }
                else if (reader.TokenType == JsonToken.EndObject) { objects.Pop(); }
                else if (reader.TokenType == JsonToken.PropertyName && !objects.Peek().Add((string)reader.Value!))
                {
                    Add(context, result, "DuplicateKey", "$." + reader.Path,
                        "同じオブジェクトに同名キーが複数あります。", "1回", (string)reader.Value!);
                }
            }
        }

        private static void Add(MasterValidationContext context, MasterValidationResult result,
            string code, string path, string message, string expected, string actual)
        {
            result.Add(new MasterValidationIssue(MasterValidationSeverity.Error, "Schema." + code,
                context.MasterName, context.FilePath, path, message, expected, actual));
        }
    }
}
