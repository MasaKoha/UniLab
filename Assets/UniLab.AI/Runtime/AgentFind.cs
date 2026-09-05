#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace UniLab.AI
{
    /// <summary>既存スナップショットの検索結果を一件一行で返します。</summary>
    internal static class AgentFind
    {
        /// <summary>観測範囲・種別・タグ除去後のラベルで絞り、推奨対象指定を添えます。</summary>
        internal static AiCommandResponse Find(UiSnapshotDocument snapshot, string label, string kind, string scope = "visible")
        {
            ValidateKind(kind);
            var filtered = UiObservationScope.Filter(snapshot, scope);
            var elements = filtered?.elements ?? Array.Empty<UiSnapshotElement>();
            var nameCounts = CountNames(snapshot?.elements ?? Array.Empty<UiSnapshotElement>());
            var query = UiInputLocator.NormalizeLabelText(label);
            var builder = new StringBuilder();
            foreach (var element in elements)
            {
                var normalizedLabel = UiInputLocator.NormalizeLabelText(element.label);
                if ((!string.IsNullOrEmpty(kind) && element.kind != kind)
                    || normalizedLabel.IndexOf(query, StringComparison.Ordinal) < 0)
                {
                    continue;
                }

                var target = element.path;
                if (nameCounts[element.name ?? string.Empty] > 1 && normalizedLabel.Length > 0)
                {
                    target = UiInputLocator.CreateLabelTargetSpec(normalizedLabel, normalizedLabel.Length);
                }

                AppendElement(builder, element, normalizedLabel, target);
            }

            return new AiCommandResponse
            {
                ok = true,
                op = "agent.find",
                text = builder.ToString().TrimEnd('\n'),
                message = builder.Length == 0 ? "見つかりません" : "検索しました。",
            };
        }

        private static Dictionary<string, int> CountNames(UiSnapshotElement[] elements)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var element in elements)
            {
                if (element == null)
                {
                    continue;
                }

                var name = element.name ?? string.Empty;
                counts.TryGetValue(name, out var count);
                counts[name] = count + 1;
            }

            return counts;
        }

        private static void AppendElement(StringBuilder builder, UiSnapshotElement element, string label, string target)
        {
            builder.Append(element.kind).Append(' ').Append(Escape(element.path));
            builder.Append(" label=\"").Append(Escape(label)).Append('"');
            builder.Append(" interactable=").Append(element.interactable ? "true" : "false");
            builder.Append(" blockedBy=\"").Append(Escape(element.blockedBy)).Append('"');
            builder.Append(" clipped=").Append(element.clipped ? "true" : "false");
            builder.Append(" rect=[");
            var separator = string.Empty;
            foreach (var coordinate in element.rect ?? Array.Empty<float>())
            {
                builder.Append(separator).Append(coordinate.ToString("0.###", CultureInfo.InvariantCulture));
                separator = ",";
            }

            builder.Append("] → submit:\"").Append(Escape(target)).Append("\"\n");
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private static void ValidateKind(string kind)
        {
            switch (kind)
            {
                case null:
                case "":
                case "Button":
                case "Text":
                case "Toggle":
                case "Input":
                case "Selectable":
                    return;
                default:
                    throw new ArgumentException("kind は Button / Text / Toggle / Input / Selectable を指定してください。", nameof(kind));
            }
        }
    }
}
#endif
