#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace UniLab.AI
{
    /// <summary>JsonUtility が許容する壊れた JSON と省略フィールドを入口で区別します。</summary>
    internal sealed class AiJsonObject
    {
        private const int MaximumDepth = 64;
        private const string NumberPattern = @"\G-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?";
        private readonly string _json;
        private int _position;

        private AiJsonObject(string json)
        {
            _json = json;
        }

        internal static Dictionary<string, string> Parse(string json)
        {
            var parser = new AiJsonObject(json);
            var members = new Dictionary<string, string>(StringComparer.Ordinal);
            parser.ReadObject(0, members);
            parser.SkipWhitespace();
            if (parser._position != json.Length)
            {
                throw new FormatException("JSON の末尾に余分な文字があります。");
            }

            return members;
        }

        internal static List<string> ParseObjectArray(string json)
        {
            var parser = new AiJsonObject(json);
            var objects = new List<string>();
            parser.Require('[');
            if (parser.Consume(']'))
            {
                return objects;
            }

            do
            {
                parser.SkipWhitespace();
                var start = parser._position;
                parser.ReadObject(0);
                objects.Add(json.Substring(start, parser._position - start));
            }
            while (parser.Consume(','));
            parser.Require(']');
            parser.SkipWhitespace();
            if (parser._position != json.Length)
            {
                throw new FormatException("JSON の末尾に余分な文字があります。");
            }

            return objects;
        }

        private void ReadObject(int depth, Dictionary<string, string> members = null)
        {
            Require('{');
            if (Consume('}'))
            {
                return;
            }

            do
            {
                SkipWhitespace();
                var keyStart = _position;
                ReadString();
                var keyJson = _json.Substring(keyStart, _position - keyStart);
                Require(':');
                SkipWhitespace();
                var valueStart = _position;
                ReadValue(depth + 1);
                if (members != null)
                {
                    var key = JsonUtility.FromJson<JsonKey>("{\"value\":" + keyJson + "}").value;
                    members.Add(key, _json.Substring(valueStart, _position - valueStart));
                }
            }
            while (Consume(','));
            Require('}');
        }

        private void ReadArray(int depth)
        {
            Require('[');
            if (Consume(']'))
            {
                return;
            }

            do
            {
                ReadValue(depth + 1);
            }
            while (Consume(','));
            Require(']');
        }

        private void ReadValue(int depth)
        {
            if (depth > MaximumDepth)
            {
                throw new FormatException("JSON のネストが深すぎます。");
            }

            SkipWhitespace();
            switch (Peek())
            {
                case '{': ReadObject(depth); return;
                case '[': ReadArray(depth); return;
                case '"': ReadString(); return;
                case 't': ReadLiteral("true"); return;
                case 'f': ReadLiteral("false"); return;
                case 'n': ReadLiteral("null"); return;
                default: ReadNumber(); return;
            }
        }

        private void ReadString()
        {
            Require('"');
            while (_position < _json.Length)
            {
                var character = _json[_position++];
                if (character == '"')
                {
                    return;
                }

                if (character < ' ')
                {
                    throw new FormatException("JSON 文字列に制御文字があります。");
                }

                if (character == '\\')
                {
                    ReadEscape();
                }
            }

            throw new FormatException("JSON 文字列が閉じていません。");
        }

        private void ReadEscape()
        {
            var character = Peek();
            _position++;
            if (character == 'u')
            {
                ReadUnicodeEscape();
                return;
            }

            if ("\"\\/bfnrt".IndexOf(character) < 0)
            {
                throw new FormatException("JSON のエスケープが不正です。");
            }
        }

        private void ReadUnicodeEscape()
        {
            const int UnicodeDigitCount = 4;
            for (var digitIndex = 0; digitIndex < UnicodeDigitCount; digitIndex++)
            {
                if (!Uri.IsHexDigit(Peek()))
                {
                    throw new FormatException("JSON の Unicode エスケープが不正です。");
                }

                _position++;
            }
        }

        private void ReadNumber()
        {
            var match = new Regex(NumberPattern, RegexOptions.CultureInvariant).Match(_json, _position);
            if (!match.Success)
            {
                throw new FormatException("JSON の値が不正です。");
            }

            _position += match.Length;
        }

        private void ReadLiteral(string literal)
        {
            if (_position + literal.Length > _json.Length ||
                string.CompareOrdinal(_json, _position, literal, 0, literal.Length) != 0)
            {
                throw new FormatException("JSON の値が不正です。");
            }

            _position += literal.Length;
        }

        private char Peek()
        {
            if (_position >= _json.Length)
            {
                throw new FormatException("JSON が途中で終わっています。");
            }

            return _json[_position];
        }

        private void Require(char character)
        {
            if (!Consume(character))
            {
                throw new FormatException($"JSON に {character} が必要です。");
            }
        }

        private bool Consume(char character)
        {
            SkipWhitespace();
            if (_position >= _json.Length || _json[_position] != character)
            {
                return false;
            }

            _position++;
            return true;
        }

        private void SkipWhitespace()
        {
            while (_position < _json.Length && " \t\r\n".IndexOf(_json[_position]) >= 0)
            {
                _position++;
            }
        }

        [Serializable]
        private sealed class JsonKey
        {
            /// <summary>エスケープ済みキーを Unity と同じ規則で復元するための値です。</summary>
            public string value;
        }
    }
}
#endif
