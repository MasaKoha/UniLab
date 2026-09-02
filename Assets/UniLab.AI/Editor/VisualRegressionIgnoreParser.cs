using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace UniLab.AI.Editor
{
    /// <summary>
    /// 設計書どおりの `capture名 -> 矩形配列` JSON を保ちつつ、追加依存なしで読み込めるようにする。
    /// </summary>
    public static class VisualRegressionIgnoreParser
    {
        /// <summary>
        /// 無視領域ファイルが無くても比較を止めず、必要時だけ最小限の除外を反映する。
        /// </summary>
        public static VisualRegressionIgnoreSettings ParseFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return new VisualRegressionIgnoreSettings
                {
                    captures = Array.Empty<VisualRegressionIgnoreRegion>(),
                };
            }

            var parser = new Parser(File.ReadAllText(filePath));
            return parser.Parse();
        }

        private sealed class Parser
        {
            private readonly string _jsonText;
            private int _position;

            public Parser(string jsonText)
            {
                _jsonText = string.IsNullOrEmpty(jsonText) ? string.Empty : jsonText;
            }

            public VisualRegressionIgnoreSettings Parse()
            {
                SkipWhitespace();
                Expect('{');

                var regions = new List<VisualRegressionIgnoreRegion>();
                SkipWhitespace();
                if (TryConsume('}'))
                {
                    return new VisualRegressionIgnoreSettings
                    {
                        captures = regions.ToArray(),
                    };
                }

                while (true)
                {
                    var captureName = ParseString();
                    SkipWhitespace();
                    Expect(':');
                    var rects = ParseRectArray();
                    regions.Add(new VisualRegressionIgnoreRegion
                    {
                        captureName = captureName,
                        rects = rects.ToArray(),
                    });

                    SkipWhitespace();
                    if (TryConsume('}'))
                    {
                        break;
                    }

                    Expect(',');
                }

                return new VisualRegressionIgnoreSettings
                {
                    captures = regions.ToArray(),
                };
            }

            private List<VisualRegressionIgnoreRect> ParseRectArray()
            {
                SkipWhitespace();
                Expect('[');

                var rects = new List<VisualRegressionIgnoreRect>();
                SkipWhitespace();
                if (TryConsume(']'))
                {
                    return rects;
                }

                while (true)
                {
                    rects.Add(ParseRect());
                    SkipWhitespace();
                    if (TryConsume(']'))
                    {
                        break;
                    }

                    Expect(',');
                }

                return rects;
            }

            private VisualRegressionIgnoreRect ParseRect()
            {
                SkipWhitespace();
                Expect('{');

                var rect = new VisualRegressionIgnoreRect();
                var hasX = false;
                var hasY = false;
                var hasWidth = false;
                var hasHeight = false;

                while (true)
                {
                    var key = ParseString();
                    SkipWhitespace();
                    Expect(':');
                    var value = ParseInt();
                    switch (key)
                    {
                        case "x":
                            rect.x = value;
                            hasX = true;
                            break;
                        case "y":
                            rect.y = value;
                            hasY = true;
                            break;
                        case "width":
                            rect.width = value;
                            hasWidth = true;
                            break;
                        case "height":
                            rect.height = value;
                            hasHeight = true;
                            break;
                        default:
                            throw new FormatException($"未知の ignore 矩形キーです。 key={key}");
                    }

                    SkipWhitespace();
                    if (TryConsume('}'))
                    {
                        break;
                    }

                    Expect(',');
                }

                if (!hasX || !hasY || !hasWidth || !hasHeight)
                {
                    throw new FormatException("ignore 矩形は x / y / width / height をすべて含む必要があります。");
                }

                return rect;
            }

            private int ParseInt()
            {
                SkipWhitespace();
                var startPosition = _position;
                if (Peek() == '-')
                {
                    _position++;
                }

                while (_position < _jsonText.Length && char.IsDigit(_jsonText[_position]))
                {
                    _position++;
                }

                var numberText = _jsonText.Substring(startPosition, _position - startPosition);
                if (!int.TryParse(numberText, out var value))
                {
                    throw new FormatException($"整数の解析に失敗しました。 value={numberText}");
                }

                return value;
            }

            private string ParseString()
            {
                SkipWhitespace();
                Expect('"');

                var builder = new StringBuilder();
                while (_position < _jsonText.Length)
                {
                    var current = _jsonText[_position++];
                    if (current == '"')
                    {
                        return builder.ToString();
                    }

                    if (current != '\\')
                    {
                        builder.Append(current);
                        continue;
                    }

                    if (_position >= _jsonText.Length)
                    {
                        throw new FormatException("文字列エスケープが途中で終わっています。");
                    }

                    var escaped = _jsonText[_position++];
                    switch (escaped)
                    {
                        case '"':
                        case '\\':
                        case '/':
                            builder.Append(escaped);
                            break;
                        case 'b':
                            builder.Append('\b');
                            break;
                        case 'f':
                            builder.Append('\f');
                            break;
                        case 'n':
                            builder.Append('\n');
                            break;
                        case 'r':
                            builder.Append('\r');
                            break;
                        case 't':
                            builder.Append('\t');
                            break;
                        case 'u':
                            builder.Append(ParseUnicodeEscape());
                            break;
                        default:
                            throw new FormatException($"未対応のエスケープです。 value=\\{escaped}");
                    }
                }

                throw new FormatException("文字列が閉じられていません。");
            }

            private char ParseUnicodeEscape()
            {
                if (_position + 4 > _jsonText.Length)
                {
                    throw new FormatException("Unicode エスケープが途中で終わっています。");
                }

                var hex = _jsonText.Substring(_position, 4);
                _position += 4;
                return (char)Convert.ToInt32(hex, 16);
            }

            private void SkipWhitespace()
            {
                while (_position < _jsonText.Length && char.IsWhiteSpace(_jsonText[_position]))
                {
                    _position++;
                }
            }

            private char Peek()
            {
                if (_position >= _jsonText.Length)
                {
                    return '\0';
                }

                return _jsonText[_position];
            }

            private bool TryConsume(char character)
            {
                SkipWhitespace();
                if (Peek() != character)
                {
                    return false;
                }

                _position++;
                return true;
            }

            private void Expect(char character)
            {
                SkipWhitespace();
                if (Peek() != character)
                {
                    throw new FormatException($"'{character}' を期待しましたが '{Peek()}' でした。");
                }

                _position++;
            }
        }
    }
}
