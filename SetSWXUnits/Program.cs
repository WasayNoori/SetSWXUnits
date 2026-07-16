using SetSWXUnits;
using SolidWorks.Interop.swconst;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

Console.WriteLine("CAD Services - Bulk Operations");
Console.WriteLine("1. Set Units");
Console.WriteLine("2. Swap Sheet Formats");
Console.WriteLine("3. Inspect Sheet Formats (dry run, single file)");
Console.WriteLine("4. List unique Sheet Sizes & Formats used in a folder (CSV)");
Console.WriteLine("5. List Sheet Sizes & Formats per drawing, no dedupe (CSV)");
Console.WriteLine("6. Replace Sheet Formats using TemplateMap.csv (Section/Lesson/DrawingSize)");
Console.WriteLine("7. Diagnose single file (hardcoded values in testing.cs)");
Console.Write("Select an operation: ");
var choice = Console.ReadLine()?.Trim();

switch (choice)
{
    case "1":
        RunSetUnits();
        break;
    case "2":
        RunSwapSheetFormats();
        break;
    case "3":
        RunInspectSheetFormats();
        break;
    case "4":
        RunListUniqueSheetSizesAndFormats();
        break;
    case "5":
        RunListSheetSizesAndFormatsPerDrawing();
        break;
    case "6":
        RunReplaceSheetFormatsByTemplateMap();
        break;
    case "7":
        Testing.RunSingleFileDiagnosis();
        break;
    default:
        Console.WriteLine("Invalid selection.");
        break;
}

Console.WriteLine("Processing completed. Press any key to exit.");
Console.ReadKey();

void RunSetUnits()
{
    var supportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".sldprt",
        ".sldasm",
        ".slddrw"
    };

    var folderPath = PromptForFolder();
    if (folderPath == null)
    {
        return;
    }

    var filesToProcess = FindFiles(folderPath, supportedExtensions);
    if (filesToProcess.Count == 0)
    {
        Console.WriteLine("No supported SolidWorks files were found in the selected folder.");
        return;
    }

    var cadService = new CADService();
    var failures = new List<string>();

    foreach (var filePath in filesToProcess)
    {
        Console.WriteLine($"Processing: {filePath}");
        try
        {
            cadService.SetUnits(filePath, 0);
        }
        catch (Exception ex)
        {
            string message = $"{filePath}: {ex.Message}";
            Console.WriteLine($"Failed to process '{filePath}'. Reason: {ex.Message}");
            failures.Add(message);
        }
    }

    WriteFailureLog(folderPath, failures);
}

void RunSwapSheetFormats()
{
    var folderPath = PromptForFolder();
    if (folderPath == null)
    {
        return;
    }

    Console.WriteLine("Enter the path to the sheet format mapping CSV file (OldFormat,NewFormat):");
    var mapPath = Console.ReadLine()?.Trim('"', ' ');

    if (string.IsNullOrWhiteSpace(mapPath) || !File.Exists(mapPath))
    {
        Console.WriteLine("The provided mapping file path is invalid or does not exist.");
        return;
    }

    Dictionary<string, string> formatMap;
    try
    {
        formatMap = SheetFormatMapLoader.Load(mapPath);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to load mapping file. Reason: {ex.Message}");
        return;
    }

    if (formatMap.Count == 0)
    {
        Console.WriteLine("The mapping file did not contain any valid entries.");
        return;
    }

    var filesToProcess = FindFiles(folderPath, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".slddrw" });
    if (filesToProcess.Count == 0)
    {
        Console.WriteLine("No .slddrw files were found in the selected folder.");
        return;
    }

    var cadService = new CADService();
    var failures = new List<string>();

    foreach (var filePath in filesToProcess)
    {
        Console.WriteLine($"Processing: {filePath}");
        try
        {
            var summary = cadService.SwapSheetFormats(filePath, formatMap);

            foreach (var message in summary.Swapped)
            {
                Console.WriteLine($"  {message}");
            }

            foreach (var message in summary.Warnings)
            {
                Console.WriteLine($"  WARNING: {message}");
                failures.Add($"{filePath}: {message}");
            }
        }
        catch (Exception ex)
        {
            string message = $"{filePath}: {ex.Message}";
            Console.WriteLine($"Failed to process '{filePath}'. Reason: {ex.Message}");
            failures.Add(message);
        }
    }

    WriteFailureLog(folderPath, failures);
}

void RunInspectSheetFormats()
{
    Console.WriteLine("Enter the path to a single .slddrw file to inspect:");
    var filePath = Console.ReadLine()?.Trim('"', ' ');

    if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath) ||
        !string.Equals(Path.GetExtension(filePath), ".slddrw", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("The provided file path is invalid or is not a .slddrw file.");
        return;
    }

    var cadService = new CADService();

    try
    {
        var sheets = cadService.InspectSheetFormats(filePath);

        foreach (var sheet in sheets)
        {
            Console.WriteLine($"Sheet '{sheet.SheetName}':");
            Console.WriteLine($"  Template file name (use this as OldFormat): {sheet.TemplateFileName}");
            Console.WriteLine($"  Template full path: {sheet.TemplatePath}");
            Console.WriteLine($"  Paper size (swDwgPaperSizes_e): {sheet.PaperSize}, First angle: {sheet.FirstAngle}");
            Console.WriteLine($"  Scale: {sheet.Scale1}:{sheet.Scale2}");
            Console.WriteLine($"  Width x Height: {sheet.Width} x {sheet.Height}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to inspect '{filePath}'. Reason: {ex.Message}");
    }
}

void RunListUniqueSheetSizesAndFormats()
{
    var folderPath = PromptForFolder();
    if (folderPath == null)
    {
        return;
    }

    var filesToProcess = FindFiles(folderPath, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".slddrw" });
    if (filesToProcess.Count == 0)
    {
        Console.WriteLine("No .slddrw files were found in the selected folder.");
        return;
    }

    var cadService = new CADService();
    var seen = new HashSet<(string SheetSize, string FormatFileName)>();
    var failures = new List<string>();

    foreach (var filePath in filesToProcess)
    {
        Console.WriteLine($"Inspecting: {filePath}");
        try
        {
            var sheetWarnings = new List<string>();
            var sheets = cadService.InspectSheetFormats(filePath, sheetWarnings);
            foreach (var sheet in sheets)
            {
                string sheetSize = GetSheetSizeName(sheet.PaperSize);
                string formatFileName = string.IsNullOrEmpty(sheet.TemplateFileName) ? "(none)" : sheet.TemplateFileName;
                seen.Add((sheetSize, formatFileName));
            }

            foreach (var warning in sheetWarnings)
            {
                Console.WriteLine($"  WARNING: {warning}");
                failures.Add($"{filePath}: {warning}");
            }
        }
        catch (Exception ex)
        {
            string message = $"{filePath}: {ex.Message}";
            Console.WriteLine($"Failed to inspect '{filePath}'. Reason: {ex.Message}");
            failures.Add(message);
        }
    }

    WriteFailureLog(folderPath, failures);

    if (seen.Count == 0)
    {
        Console.WriteLine("No sheet size/format combinations were found.");
        return;
    }

    var csvPath = Path.Combine(folderPath, "SheetSizesAndFormats.csv");
    var lines = new List<string> { "SheetSize,FormatFileName" };
    lines.AddRange(seen
        .OrderBy(s => s.SheetSize, StringComparer.OrdinalIgnoreCase)
        .ThenBy(s => s.FormatFileName, StringComparer.OrdinalIgnoreCase)
        .Select(s => $"{CsvEscape(s.SheetSize)},{CsvEscape(s.FormatFileName)}"));

    File.WriteAllLines(csvPath, lines);
    Console.WriteLine($"Wrote {seen.Count} unique sheet size/format combination(s) to '{csvPath}'.");
}

void RunListSheetSizesAndFormatsPerDrawing()
{
    var folderPath = PromptForFolder();
    if (folderPath == null)
    {
        return;
    }

    var filesToProcess = FindSectionLessonFiles(folderPath, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".slddrw" });
    if (filesToProcess.Count == 0)
    {
        Console.WriteLine("No .slddrw files were found under a two-digit Section/Lesson folder structure.");
        return;
    }

    var cadService = new CADService();
    var rows = new List<(string Section, string Lesson, string DrawingFileName, string SheetSize, string FormatFileName)>();
    var failures = new List<string>();

    foreach (var (section, lesson, filePath) in filesToProcess)
    {
        Console.WriteLine($"Inspecting: {filePath}");
        try
        {
            string drawingFileName = Path.GetFileName(filePath);
            var sheetWarnings = new List<string>();
            var sheets = cadService.InspectSheetFormats(filePath, sheetWarnings);
            foreach (var sheet in sheets)
            {
                string sheetSize = GetSheetSizeName(sheet.PaperSize);
                string formatFileName = string.IsNullOrEmpty(sheet.TemplateFileName) ? "(none)" : sheet.TemplateFileName;
                rows.Add((section, lesson, drawingFileName, sheetSize, formatFileName));
            }

            foreach (var warning in sheetWarnings)
            {
                Console.WriteLine($"  WARNING: {warning}");
                failures.Add($"{filePath}: {warning}");
            }
        }
        catch (Exception ex)
        {
            string message = $"{filePath}: {ex.Message}";
            Console.WriteLine($"Failed to inspect '{filePath}'. Reason: {ex.Message}");
            failures.Add(message);
        }
    }

    WriteFailureLog(folderPath, failures);

    if (rows.Count == 0)
    {
        Console.WriteLine("No sheet size/format combinations were found.");
        return;
    }

    var csvPath = Path.Combine(folderPath, "AllSheetSizesAndFormats.csv");
    var lines = new List<string> { "Section,Lesson,Filename,DrawingSize,SheetFormatName" };
    lines.AddRange(rows
        .OrderBy(r => r.Section, StringComparer.OrdinalIgnoreCase)
        .ThenBy(r => r.Lesson, StringComparer.OrdinalIgnoreCase)
        .ThenBy(r => r.DrawingFileName, StringComparer.OrdinalIgnoreCase)
        .Select(r => $"{CsvEscape(r.Section)},{CsvEscape(r.Lesson)},{CsvEscape(r.DrawingFileName)},{CsvEscape(r.SheetSize)},{CsvEscape(r.FormatFileName)}"));

    File.WriteAllLines(csvPath, lines);
    Console.WriteLine($"Wrote {rows.Count} row(s) to '{csvPath}'.");
}

void RunReplaceSheetFormatsByTemplateMap()
{
    var folderPath = PromptForFolder();
    if (folderPath == null)
    {
        return;
    }

    Console.WriteLine("Enter the path to TemplateMap.csv (Section,Lesson,DrawingSize,SheetFormatName,ReplaceFormat):");
    var templateMapPath = Console.ReadLine()?.Trim('"', ' ');

    if (string.IsNullOrWhiteSpace(templateMapPath) || !File.Exists(templateMapPath))
    {
        Console.WriteLine("The provided TemplateMap.csv path is invalid or does not exist.");
        return;
    }

    List<TemplateMapEntry> templateMap;
    try
    {
        templateMap = TemplateMapLoader.Load(templateMapPath);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to load TemplateMap.csv. Reason: {ex.Message}");
        return;
    }

    if (templateMap.Count == 0)
    {
        Console.WriteLine("TemplateMap.csv did not contain any valid entries.");
        return;
    }

    Console.WriteLine("Enter the folder that contains the new sheet format files:");
    var newFormatsFolder = Console.ReadLine()?.Trim('"', ' ');

    if (string.IsNullOrWhiteSpace(newFormatsFolder) || !Directory.Exists(newFormatsFolder))
    {
        Console.WriteLine("The provided new sheet formats folder is invalid or does not exist.");
        return;
    }

    var filesToProcess = FindSectionLessonFiles(folderPath, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".slddrw" });
    if (filesToProcess.Count == 0)
    {
        Console.WriteLine("No .slddrw files were found under a two-digit Section/Lesson folder structure.");
        return;
    }

    var cadService = new CADService();
    var failures = new List<string>();

    foreach (var (section, lesson, filePath) in filesToProcess)
    {
        var sizeToNewTemplatePath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in templateMap)
        {
            if (!string.Equals(entry.Section, section, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(entry.Lesson, lesson, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            sizeToNewTemplatePath[entry.DrawingSize] = Path.Combine(newFormatsFolder, entry.ReplaceFormat);
        }

        if (sizeToNewTemplatePath.Count == 0)
        {
            Console.WriteLine($"Skipping '{filePath}': no TemplateMap entries for Section '{section}', Lesson '{lesson}'.");
            continue;
        }

        Console.WriteLine($"Processing: {filePath}");
        try
        {
            var summary = cadService.SwapSheetFormatsBySize(filePath, sizeToNewTemplatePath);

            foreach (var message in summary.Swapped)
            {
                Console.WriteLine($"  {message}");
            }

            foreach (var message in summary.Warnings)
            {
                Console.WriteLine($"  WARNING: {message}");
                failures.Add($"{filePath}: {message}");
            }
        }
        catch (Exception ex)
        {
            string message = $"{filePath}: {ex.Message}";
            Console.WriteLine($"Failed to process '{filePath}'. Reason: {ex.Message}");
            failures.Add(message);
        }
    }

    WriteFailureLog(folderPath, failures);
}

void WriteFailureLog(string folderPath, List<string> failures)
{
    if (failures.Count == 0)
    {
        return;
    }

    var logPath = Path.Combine(folderPath, "FailureLog.txt");
    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    File.AppendAllLines(logPath, failures.Select(f => $"[{timestamp}] {f}"));
    Console.WriteLine($"Logged {failures.Count} failure(s) to '{logPath}'.");
}

string GetSheetSizeName(int paperSize)
{
    return Enum.IsDefined(typeof(swDwgPaperSizes_e), paperSize)
        ? ((swDwgPaperSizes_e)paperSize).ToString()
        : paperSize.ToString();
}

string CsvEscape(string value)
{
    if (value.Contains(',') || value.Contains('"'))
    {
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    return value;
}

string? PromptForFolder()
{
    Console.WriteLine("Enter the folder path that contains the SolidWorks files:");
    var folderPath = Console.ReadLine()?.Trim('"', ' ');

    if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
    {
        Console.WriteLine("The provided path is invalid or does not exist.");
        return null;
    }

    return folderPath;
}

List<string> FindFiles(string folderPath, HashSet<string> extensions)
{
    return Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
        .Where(file => extensions.Contains(Path.GetExtension(file)))
        .ToList();
}

// Folder layout: <root>\<Section 2-digit prefix>\<Lesson 2-digit prefix>\...\file
// Folders without a two-digit prefix are skipped entirely (not descended into).
List<(string Section, string Lesson, string FilePath)> FindSectionLessonFiles(string rootPath, HashSet<string> extensions)
{
    var results = new List<(string Section, string Lesson, string FilePath)>();
    CollectSectionLessonFiles(rootPath, null, null, extensions, results);
    return results;
}

void CollectSectionLessonFiles(string directoryPath, string? section, string? lesson, HashSet<string> extensions,
    List<(string Section, string Lesson, string FilePath)> results)
{
    if (section != null && lesson != null)
    {
        foreach (var file in Directory.EnumerateFiles(directoryPath))
        {
            if (extensions.Contains(Path.GetExtension(file)))
            {
                results.Add((section, lesson, file));
            }
        }
    }

    foreach (var subDirectory in Directory.EnumerateDirectories(directoryPath))
    {
        if (!TryGetTwoDigitPrefix(Path.GetFileName(subDirectory), out string prefix))
        {
            continue;
        }

        string nextSection = section ?? prefix;
        string? nextLesson = section == null ? lesson : lesson ?? prefix;

        CollectSectionLessonFiles(subDirectory, nextSection, nextLesson, extensions, results);
    }
}

bool TryGetTwoDigitPrefix(string folderName, out string prefix)
{
    if (folderName.Length >= 2 && char.IsDigit(folderName[0]) && char.IsDigit(folderName[1]))
    {
        prefix = folderName.Substring(0, 2);
        return true;
    }

    prefix = string.Empty;
    return false;
}
