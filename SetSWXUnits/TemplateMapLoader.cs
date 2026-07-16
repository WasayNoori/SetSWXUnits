using System;
using System.Collections.Generic;
using System.IO;

namespace SetSWXUnits
{
    // CSV columns: Section, Lesson, DrawingSize, SheetFormatName, ReplaceFormat (file names only, not paths).
    public static class TemplateMapLoader
    {
        public static List<TemplateMapEntry> Load(string csvPath)
        {
            var entries = new List<TemplateMapEntry>();

            foreach (var line in File.ReadAllLines(csvPath))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var parts = line.Split(',');
                if (parts.Length < 5)
                {
                    continue;
                }

                string section = parts[0].Trim().Trim('"');
                string lesson = parts[1].Trim().Trim('"');
                string drawingSize = parts[2].Trim().Trim('"');
                string sheetFormatName = parts[3].Trim().Trim('"');
                string replaceFormat = Path.GetFileName(parts[4].Trim().Trim('"'));

                if (section.Equals("Section", StringComparison.OrdinalIgnoreCase) &&
                    lesson.Equals("Lesson", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (section.Length == 0 || lesson.Length == 0 || drawingSize.Length == 0 || replaceFormat.Length == 0)
                {
                    continue;
                }

                entries.Add(new TemplateMapEntry
                {
                    Section = section,
                    Lesson = lesson,
                    DrawingSize = drawingSize,
                    SheetFormatName = sheetFormatName,
                    ReplaceFormat = replaceFormat,
                });
            }

            return entries;
        }
    }
}
