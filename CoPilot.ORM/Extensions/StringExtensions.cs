﻿using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CoPilot.ORM.Extensions
{
    public static class StringExtensions
    {
        public static string[] Tokenize(this string text)
        {
            if(string.IsNullOrEmpty(text)) throw new ArgumentException("text");

            const string strRegex = @"((?<=[a-z])[A-Z]|[A-Z](?=[a-z]))";
            const string strReplace = @" $1";
            var regex = new Regex(strRegex);

            text = regex.Replace(text, strReplace).Trim();

            return text.Split(new [] {'_', '-', ' '}, StringSplitOptions.RemoveEmptyEntries);

        }

        [Obsolete]
        public static string ToTitleCase(this string camelCase)
        {
            const string strRegex = @"([A-Z])([A-Z][a-z])|([a-z0-9])([A-Z])";
            const string strReplace = @"$1$3_$2$4";
            var myRegex = new Regex(strRegex, RegexOptions.Singleline);

            return myRegex.Replace(camelCase, strReplace).ToLower();
        }

        [Obsolete]
        public static string ToCamelCase(this string text, char seperator = '_')
        {
            var parts = text.Split(seperator);

            var camelCase = string.Empty;

            foreach (var part in parts)
            {
                if(string.IsNullOrEmpty(part)) continue;
                var t1 = part[0].ToString().ToUpperInvariant();

                var t2 = part.Length > 1 ? part.Substring(1, part.Length - 1).ToLowerInvariant() : "";
                camelCase += t1 + t2;
            }

            return camelCase;
        }

        public static string RemoveRepeatedNeighboringWords(this string text, char wordSeperator = '_')
        {
            var textSplit = text.Split(wordSeperator);

            if (textSplit.Length > 1)
            {
                var newText = textSplit[0];
                for (var i = 1; i < textSplit.Length; i++)
                {
                    if (!textSplit[i].Equals(textSplit[i - 1], StringComparison.OrdinalIgnoreCase))
                    {
                        newText += wordSeperator + textSplit[i];
                    }
                }
                return newText;
            }
            return text;
        }
    }
}
