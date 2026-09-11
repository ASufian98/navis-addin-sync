using System;
using System.Collections.Generic;
using System.IO;

namespace NavisWebAppSync
{
    /// <summary>
    /// Parser for the channel .env files (.env.local / .env.staging /
    /// .env.production) that the csproj embeds as resources.
    /// </summary>
    public static class EnvFile
    {
        /// <summary>
        /// Key=value lines; `#` comments and blanks skipped, surrounding
        /// double quotes stripped, keys compared case-insensitively.
        /// </summary>
        public static Dictionary<string, string> Parse(TextReader reader)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (reader == null) return map;

            string line;
            while ((line = reader.ReadLine()) != null)
            {
                var t = line.Trim();
                if (t.Length == 0 || t.StartsWith("#")) continue;
                var eq = t.IndexOf('=');
                if (eq <= 0) continue;
                var k = t.Substring(0, eq).Trim();
                var val = t.Substring(eq + 1).Trim().Trim('"');
                map[k] = val;
            }
            return map;
        }

        public static Dictionary<string, string> Parse(string text) =>
            Parse(new StringReader(text ?? ""));
    }
}
