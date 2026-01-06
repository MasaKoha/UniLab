using System.Collections.Generic;
using System.Text;

namespace UniLab.Common.Utility
{
    public static class StringUtility
    {
        public static List<string> ParseCsvLine(this string line)
        {
            var result = new List<string>();
            var sb = new StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            sb.Append('"');
                            i++; // skip escaped quote
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
                else
                {
                    switch (c)
                    {
                        case ',':
                            result.Add(sb.ToString());
                            sb.Clear();
                            break;
                        case '"':
                            inQuotes = true;
                            break;
                        default:
                            sb.Append(c);
                            break;
                    }
                }
            }

            result.Add(sb.ToString());
            return result;
        }
    }
}