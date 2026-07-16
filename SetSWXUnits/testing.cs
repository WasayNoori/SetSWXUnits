using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SetSWXUnits
{
    // Ad-hoc single-file diagnosis harness. Edit the constants below to point at whichever
    // file/TemplateMap/Section/Lesson combination you're trying to reproduce, then run it via
    // the "Diagnose single file" menu option.
    public static class Testing
    {
        private const string TestFile = @"C:\Translations\Essential Drawings\German\SOLIDWORKS Zeichnungen 2024 - Course Files_mapped\SOLIDWORKS Zeichnungen\TESTING\07 Annotations\03 Hole Callouts\Hole Callouts.SLDDRW";
        private const string TemplateMapPath = @"C:\Translations\Essential Drawings\German\SOLIDWORKS Zeichnungen 2024 - Course Files_mapped\SOLIDWORKS Zeichnungen\TemplateMap.csv";
        private const string TemplatesFolder = @"C:\Translations\Essential Drawings\ISO Formats";
        private const string Section = "07";
        private const string Lesson = "03";

        public static void RunSingleFileDiagnosis()
        {
            Console.WriteLine("=== Single-file diagnosis ===");
            Console.WriteLine($"File: {TestFile}");
            Console.WriteLine($"TemplateMap: {TemplateMapPath}");
            Console.WriteLine($"Templates folder: {TemplatesFolder}");
            Console.WriteLine($"Section/Lesson override: {Section}/{Lesson}");
            Console.WriteLine();

            if (!File.Exists(TestFile))
            {
                Console.WriteLine($"File not found: {TestFile}");
                return;
            }

            if (!File.Exists(TemplateMapPath))
            {
                Console.WriteLine($"TemplateMap.csv not found: {TemplateMapPath}");
                return;
            }

            if (!Directory.Exists(TemplatesFolder))
            {
                Console.WriteLine($"Templates folder not found: {TemplatesFolder}");
                return;
            }

            List<TemplateMapEntry> templateMap;
            try
            {
                templateMap = TemplateMapLoader.Load(TemplateMapPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load TemplateMap.csv. Reason: {ex.Message}");
                return;
            }

            Console.WriteLine($"Loaded {templateMap.Count} TemplateMap entries.");

            var matchingEntries = templateMap
                .Where(e => string.Equals(e.Section, Section, StringComparison.OrdinalIgnoreCase)
                         && string.Equals(e.Lesson, Lesson, StringComparison.OrdinalIgnoreCase))
                .ToList();

            Console.WriteLine($"Entries matching Section '{Section}', Lesson '{Lesson}': {matchingEntries.Count}");
            foreach (var entry in matchingEntries)
            {
                Console.WriteLine($"  DrawingSize='{entry.DrawingSize}' SheetFormatName='{entry.SheetFormatName}' ReplaceFormat='{entry.ReplaceFormat}'");
            }

            if (matchingEntries.Count == 0)
            {
                Console.WriteLine("No matching TemplateMap entries. Nothing to swap.");
                return;
            }

            var sizeToNewTemplatePath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in matchingEntries)
            {
                var fullTemplatePath = Path.Combine(TemplatesFolder, entry.ReplaceFormat);
                sizeToNewTemplatePath[entry.DrawingSize] = fullTemplatePath;
                Console.WriteLine($"  Map: '{entry.DrawingSize}' -> '{fullTemplatePath}' (exists: {File.Exists(fullTemplatePath)})");
            }

            Console.WriteLine();
            var cadService = new CADService();

            Console.WriteLine("Inspecting current sheet formats BEFORE swap...");
            if (!TryInspect(cadService, "before"))
            {
                return;
            }

            Console.WriteLine();
            Console.WriteLine("Running SwapSheetFormatsBySize...");
            try
            {
                var summary = cadService.SwapSheetFormatsBySize(TestFile, sizeToNewTemplatePath);

                Console.WriteLine("--- Swapped ---");
                foreach (var message in summary.Swapped)
                {
                    Console.WriteLine($"  {message}");
                }

                Console.WriteLine("--- Warnings ---");
                foreach (var message in summary.Warnings)
                {
                    Console.WriteLine($"  {message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SwapSheetFormatsBySize threw an exception: {ex}");
                return;
            }

            Console.WriteLine();
            Console.WriteLine("Inspecting sheet formats AFTER swap...");
            TryInspect(cadService, "after");

            Console.WriteLine();
            Console.WriteLine("=== Diagnosis complete ===");
        }

        private static bool TryInspect(CADService cadService, string label)
        {
            try
            {
                var warnings = new List<string>();
                var sheets = cadService.InspectSheetFormats(TestFile, warnings);

                foreach (var sheet in sheets)
                {
                    Console.WriteLine($"  Sheet '{sheet.SheetName}': PaperSize={sheet.PaperSize}, Template='{sheet.TemplateFileName}'");
                }

                foreach (var warning in warnings)
                {
                    Console.WriteLine($"  WARNING: {warning}");
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to inspect '{TestFile}' ({label}). Reason: {ex.Message}");
                return false;
            }
        }
    }
}
