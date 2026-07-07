using SetSWXUnits;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

Console.WriteLine("CAD Services - Bulk Operations");
Console.WriteLine("1. Set Units");
Console.WriteLine("2. Swap Sheet Formats");
Console.WriteLine("3. Inspect Sheet Formats (dry run, single file)");
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

    foreach (var filePath in filesToProcess)
    {
        Console.WriteLine($"Processing: {filePath}");
        try
        {
            cadService.SetUnits(filePath, 0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to process '{filePath}'. Reason: {ex.Message}");
        }
    }
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
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to process '{filePath}'. Reason: {ex.Message}");
        }
    }
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
