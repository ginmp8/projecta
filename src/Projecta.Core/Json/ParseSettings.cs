using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Projecta.Json
{
    public sealed class ParseSettings
    {
        internal static readonly ParseSettings DefaultSettings = new ParseSettings() {
            AllowTrailingComma = true,
            AllowSinglequoteStrings = true,
            AllowAllWhitespace = true,
            AllowUnescapedControlChars = true,
        };

        private static readonly ParseSettings s_strictSettings = new ParseSettings() {
            AllowTrailingComma = false,
            AllowSinglequoteStrings = false,
            AllowAllWhitespace = false,
            AllowUnescapedControlChars = false,
            AllowUndefined = false,
        };

        public static ParseSettings GetDefault()
        {
            return DefaultSettings.Clone();
        }

        public static ParseSettings GetStrict()
        {
            return s_strictSettings.Clone();
        }

        /** <summary>allows [1,]</summary>*/
        public bool AllowTrailingComma { get; set; } = true;

        /** <summary>allows {'a': 'b'}</summary>*/
        public bool AllowSinglequoteStrings { get; set; } = true;

        /** <summary>allows all unicode whitespace chars as whitespace. When false - only  0x20 (space), 0x09 (tab), 0x0A (line feed) and 0x0D (carriage return) are allowed as per RFC 8259</summary>*/
        public bool AllowAllWhitespace { get; set; } = true;

        /** <summary>allows unescaped chars in range 0x00..0x1F in strings</summary>*/
        public bool AllowUnescapedControlChars { get; set; } = true;


        public int MaxDepth { get; set; } = 64;
        public int MaxTokens { get; set; } = 100_000;
        public int MaxCharacters { get; set; } = 2_000_000;
        public int MaxStringLength { get; set; } = 1_000_000;
        public bool AllowUndefined { get; set; } = true;

        internal void ValidateLimits()
        {
            if (MaxDepth is < 1 or > 256 || MaxTokens < 1 || MaxCharacters < 1 || MaxStringLength < 1)
                throw new ArgumentOutOfRangeException(nameof(ParseSettings));
        }

        public ParseSettings Clone()
        {
            return (ParseSettings)this.MemberwiseClone();
        }

        public bool IsWhiteSpace(char c)
        {
            if (this.AllowAllWhitespace)
            {
                return Char.IsWhiteSpace(c);
            }
            else
            {
                switch (c)
                {
                case (char)0x20:
                case (char)0x09:
                case (char)0x0A:
                case (char)0x0D:
                    return true;
                default:
                    return false;
                }
            }
        }
    }
}
