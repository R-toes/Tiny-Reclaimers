using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace TrashTD.Editor
{
    public enum LoreIssueSeverity { Info, Warning }

    public sealed class LoreIssue
    {
        public readonly LoreIssueSeverity severity;
        public readonly string message;

        public LoreIssue(LoreIssueSeverity severity, string message)
        {
            this.severity = severity;
            this.message = message;
        }
    }

    /// <summary>
    /// The rules behind the Lore &amp; Text Sheet window, kept apart from the GUI so they can be tested: what is worth flagging in a piece
    /// of text, find and replace, and reading and writing the CSV the sheet exports for editing in a spreadsheet.
    /// </summary>
    public static class LoreTextRules
    {
        /// <summary>
        /// Names units used to have. Text that still mentions one is almost certainly out of date after a rename. Matched as whole words,
        /// case-sensitively, so an ordinary word like "bulkhead" in lowercase is not flagged.
        /// </summary>
        public static readonly string[] DefaultOldTerms =
        {
            "Pyrolite", "Chillpath", "Shadeslice", "Proxishot", "Bulkhead", "Chi Paw", "Ignisaur"
        };

        private static readonly string[] PlaceholderWords = { "TODO", "TBD", "FIXME", "lorem ipsum", "placeholder" };

        // ============================
        // Checks
        // ============================

        /// <summary>
        /// What is worth a second look in one field. <paramref name="softLimit"/> is a length above which the text is probably too long for
        /// the screen it appears on (0 = no limit); <paramref name="required"/> flags an empty field.
        /// </summary>
        public static List<LoreIssue> Check(string text, int softLimit, bool required, IList<string> oldTerms)
        {
            var issues = new List<LoreIssue>();
            if (string.IsNullOrWhiteSpace(text))
            {
                if (required) issues.Add(new LoreIssue(LoreIssueSeverity.Warning, "Empty."));
                return issues;
            }

            if (text != text.Trim()) issues.Add(new LoreIssue(LoreIssueSeverity.Info, "Has a space or line break at the start or end."));
            if (text.Contains("  ")) issues.Add(new LoreIssue(LoreIssueSeverity.Info, "Has a double space."));
            if (softLimit > 0 && text.Length > softLimit)
                issues.Add(new LoreIssue(LoreIssueSeverity.Info, "Long: " + text.Length + " characters (usually under " + softLimit + ")."));

            if (oldTerms != null)
            {
                foreach (string term in oldTerms)
                {
                    if (string.IsNullOrWhiteSpace(term)) continue;
                    if (CountMatches(text, term.Trim(), true, true) > 0)
                        issues.Add(new LoreIssue(LoreIssueSeverity.Warning, "Mentions the old name \"" + term.Trim() + "\"."));
                }
            }

            foreach (string word in PlaceholderWords)
            {
                if (text.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
                    issues.Add(new LoreIssue(LoreIssueSeverity.Warning, "Looks like placeholder text (\"" + word + "\")."));
            }
            return issues;
        }

        public static int WordCount(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            return text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Length;
        }

        // ============================
        // Find and replace
        // ============================

        public static Regex BuildRegex(string find, bool matchCase, bool wholeWord)
        {
            string pattern = Regex.Escape(find);
            if (wholeWord) pattern = @"(?<![\p{L}\p{N}_])" + pattern + @"(?![\p{L}\p{N}_])";
            RegexOptions options = matchCase ? RegexOptions.CultureInvariant : RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;
            return new Regex(pattern, options);
        }

        public static int CountMatches(string text, string find, bool matchCase, bool wholeWord)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(find)) return 0;
            return BuildRegex(find, matchCase, wholeWord).Matches(text).Count;
        }

        /// <summary>Replaces every match; the replacement is used literally (no regex group syntax).</summary>
        public static string Replace(string text, string find, string replacement, bool matchCase, bool wholeWord, out int count)
        {
            count = 0;
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(find)) return text;

            int replaced = 0;
            string result = BuildRegex(find, matchCase, wholeWord).Replace(text, match =>
            {
                replaced++;
                return replacement ?? string.Empty;
            });
            count = replaced;
            return result;
        }

        // ============================
        // CSV (RFC 4180: quotes around fields with commas, quotes or line breaks; a quote inside is doubled)
        // ============================

        public static string CsvEscape(string field)
        {
            field = field ?? string.Empty;
            bool needsQuotes = field.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 || field != field.Trim();
            return needsQuotes ? "\"" + field.Replace("\"", "\"\"") + "\"" : field;
        }

        public static string WriteCsv(IEnumerable<string[]> rows)
        {
            var builder = new StringBuilder();
            foreach (string[] row in rows)
            {
                for (int i = 0; i < row.Length; i++)
                {
                    if (i > 0) builder.Append(',');
                    builder.Append(CsvEscape(row[i]));
                }
                builder.Append("\r\n");
            }
            return builder.ToString();
        }

        /// <summary>Reads CSV text into rows of fields. Line breaks inside quoted fields are kept (as \n); a leading byte-order mark is ignored.</summary>
        public static List<string[]> ParseCsv(string csv)
        {
            var rows = new List<string[]>();
            if (string.IsNullOrEmpty(csv)) return rows;
            if (csv[0] == '﻿') csv = csv.Substring(1);

            var fields = new List<string>();
            var field = new StringBuilder();
            bool inQuotes = false;
            bool anyContent = false;

            for (int i = 0; i < csv.Length; i++)
            {
                char c = csv[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < csv.Length && csv[i + 1] == '"') { field.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else if (c == '\r')
                    {
                        if (i + 1 < csv.Length && csv[i + 1] == '\n') i++;
                        field.Append('\n');
                    }
                    else field.Append(c);
                    continue;
                }

                switch (c)
                {
                    case '"':
                        inQuotes = true;
                        anyContent = true;
                        break;
                    case ',':
                        fields.Add(field.ToString());
                        field.Length = 0;
                        anyContent = true;
                        break;
                    case '\r':
                    case '\n':
                        if (c == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n') i++;
                        if (anyContent || field.Length > 0)
                        {
                            fields.Add(field.ToString());
                            rows.Add(fields.ToArray());
                        }
                        fields.Clear();
                        field.Length = 0;
                        anyContent = false;
                        break;
                    default:
                        field.Append(c);
                        anyContent = true;
                        break;
                }
            }

            if (anyContent || field.Length > 0)
            {
                fields.Add(field.ToString());
                rows.Add(fields.ToArray());
            }
            return rows;
        }

        /// <summary>Text compared the way the sheet compares it: line endings do not matter.</summary>
        public static string NormalizeLineEndings(string text)
        {
            return (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
        }
    }
}
