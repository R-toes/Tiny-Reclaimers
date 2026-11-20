using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TrashTD.Playtest
{
    /// <summary>
    /// A tiny JSON object writer for the "data" column. Unity's JsonUtility cannot write free-form objects, and a
    /// sheet cell only needs flat keys with a few counts, so this builds the text directly. Text is escaped and cut to
    /// <see cref="MaxTextLength"/> so one event never gets huge.
    /// </summary>
    public sealed class PlaytestJson
    {
        public const int MaxTextLength = 300;

        private readonly StringBuilder text = new StringBuilder("{");
        private bool empty = true;

        public PlaytestJson Add(string key, string value)
        {
            Key(key);
            text.Append('"').Append(Escape(value, MaxTextLength)).Append('"');
            return this;
        }

        public PlaytestJson Add(string key, int value)
        {
            Key(key);
            text.Append(value.ToString(CultureInfo.InvariantCulture));
            return this;
        }

        public PlaytestJson Add(string key, float value)
        {
            Key(key);
            text.Append(float.IsNaN(value) || float.IsInfinity(value) ? "0" : value.ToString("0.##", CultureInfo.InvariantCulture));
            return this;
        }

        public PlaytestJson Add(string key, bool value)
        {
            Key(key);
            text.Append(value ? "true" : "false");
            return this;
        }

        /// <summary>A nested object of counts, e.g. {"Guard":2,"Sniper":1}. Nothing is added when there are none.</summary>
        public PlaytestJson AddCounts(string key, IDictionary<string, int> counts)
        {
            if (counts == null || counts.Count == 0) return this;
            Key(key);
            text.Append('{');
            bool first = true;
            foreach (KeyValuePair<string, int> pair in counts)
            {
                if (!first) text.Append(',');
                first = false;
                text.Append('"').Append(Escape(pair.Key, 60)).Append("\":").Append(pair.Value.ToString(CultureInfo.InvariantCulture));
            }
            text.Append('}');
            return this;
        }

        /// <summary>A list of names, e.g. ["Guard","Sniper"].</summary>
        public PlaytestJson AddList(string key, IList<string> values)
        {
            if (values == null || values.Count == 0) return this;
            Key(key);
            text.Append('[');
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0) text.Append(',');
                text.Append('"').Append(Escape(values[i], 60)).Append('"');
            }
            text.Append(']');
            return this;
        }

        public override string ToString() => text + "}";

        private void Key(string key)
        {
            if (!empty) text.Append(',');
            empty = false;
            text.Append('"').Append(Escape(key, 40)).Append("\":");
        }

        /// <summary>Makes <paramref name="value"/> safe inside a JSON string and cuts it to <paramref name="maxLength"/> characters.</summary>
        public static string Escape(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (value.Length > maxLength) value = value.Substring(0, maxLength);

            var builder = new StringBuilder(value.Length + 8);
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': break;
                    case '\t': builder.Append(' '); break;
                    default:
                        if (c >= ' ') builder.Append(c);
                        break;
                }
            }
            return builder.ToString();
        }
    }
}
