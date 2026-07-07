using System;
using System.Collections.Generic;
using System.IO;

namespace SetSWXUnits
{
    public static class SheetFormatMapLoader
    {
        public static Dictionary<string, string> Load(string csvPath)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var line in File.ReadAllLines(csvPath))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var parts = line.Split(',');
                if (parts.Length < 2)
                {
                    continue;
                }

                string oldFormat = Path.GetFileName(parts[0].Trim().Trim('"'));
                string newFormat = parts[1].Trim().Trim('"');

                if (oldFormat.Equals("OldFormat", StringComparison.OrdinalIgnoreCase) &&
                    newFormat.Equals("NewFormat", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (oldFormat.Length == 0 || newFormat.Length == 0)
                {
                    continue;
                }

                map[oldFormat] = newFormat;
            }

            return map;
        }
    }
}
