using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
namespace SetSWXUnits
{
   public  class CADService
    {

        public void SetUnits(string filePath,int unitType)
        {
            //get extension
            string extnsion = System.IO.Path.GetExtension(filePath).ToLower();
            extnsion=extnsion?.ToLower();
            SldWorks swapp;
            swapp= SolidworksSingleton.getApplication();
            ModelDoc2 swModel;
          
            switch (extnsion)
            {
                case ".sldprt":
                    swModel=swapp.OpenDoc6(filePath, (int)swDocumentTypes_e.swDocPART, (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", 0, 0);
                    break;
                case ".sldasm":
                   swModel= swapp.OpenDoc6(filePath, (int)swDocumentTypes_e.swDocASSEMBLY, (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", 0, 0);
                    break;
                case ".slddrw":
                    swModel= swapp.OpenDoc6(filePath, (int)swDocumentTypes_e.swDocDRAWING, (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", 0, 0);
                    break;
                default:
                    throw new NotSupportedException($"File extension '{extnsion}' is not supported.");
            }
           
            if (swModel == null)
            {
                throw new InvalidOperationException("No active document found.");
            }
           
            int errors = 0;
            int warnings = 0;
            // Set the units based on the provided unitType
            swModel.Extension.SetUserPreferenceInteger((int)swUserPreferenceIntegerValue_e.swUnitSystem, 0,
                (int)swUnitSystem_e.swUnitSystem_Custom);


            swModel.Extension.SetUserPreferenceInteger((int)swUserPreferenceIntegerValue_e.swUnitsLinear,0,(int)swLengthUnit_e.swMM);

            swModel.Extension.SetUserPreferenceInteger((int)swUserPreferenceIntegerValue_e.swUnitsMassPropMass, 0,
                (int)swUnitsMassPropMass_e.swUnitsMassPropMass_Grams);

            
            //If this is a drawing, we also want to set the template to ISO
            if (extnsion == ".slddrw")
            {
                swModel.Extension.SetUserPreferenceInteger((int)swUserPreferenceIntegerValue_e.swDetailingDimensionStandard, 0, (int)swDetailingStandard_e.swDetailingStandardISO);
            }

            swModel.EditRebuild3();
            // Optionally, you can check for errors or warnings after setting units
            if (errors > 0 || warnings > 0)
            {
                throw new InvalidOperationException($"Failed to set units. Errors: {errors}, Warnings: {warnings}");
            }

            swModel.Save();
            swapp.CloseDoc(swModel.GetTitle());

        }

        public SheetFormatSwapSummary SwapSheetFormats(string filePath, IReadOnlyDictionary<string, string> formatMap)
        {
            string extension = System.IO.Path.GetExtension(filePath)?.ToLowerInvariant() ?? string.Empty;
            if (extension != ".slddrw")
            {
                throw new NotSupportedException($"File extension '{extension}' is not supported for sheet format swapping. Only .slddrw files are supported.");
            }

            SldWorks swapp = SolidworksSingleton.getApplication();
            ModelDoc2 swModel = swapp.OpenDoc6(filePath, (int)swDocumentTypes_e.swDocDRAWING, (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", 0, 0);

            if (swModel == null)
            {
                throw new InvalidOperationException($"Failed to open '{filePath}'.");
            }

            var summary = new SheetFormatSwapSummary();
            DrawingDoc swDraw = (DrawingDoc)swModel;

            string[] sheetNames = (string[])swDraw.GetSheetNames();
            var pendingConfirmations = new List<(string SheetName, string OldTemplate, string NewTemplate)>();

            foreach (string sheetName in sheetNames)
            {
                try
                {
                    if (!swDraw.ActivateSheet(sheetName))
                    {
                        summary.Warnings.Add($"Sheet '{sheetName}': could not activate, skipped.");
                        continue;
                    }

                    Sheet swSheet = (Sheet)swDraw.GetCurrentSheet();
                    string currentTemplate = swSheet.GetTemplateName();
                    string currentTemplateFileName = System.IO.Path.GetFileName(currentTemplate);

                    if (string.IsNullOrEmpty(currentTemplateFileName) || !formatMap.TryGetValue(currentTemplateFileName, out string? newTemplate))
                    {
                        summary.Warnings.Add($"Sheet '{sheetName}': format '{currentTemplateFileName}' not found in mapping, skipped.");
                        continue;
                    }

                    double[] props = (double[])swSheet.GetProperties2();

                    int paperSize = Convert.ToInt32(props[0]);
                    int templateIn = Convert.ToInt32(props[1]);
                    double scale1 = Convert.ToDouble(props[2]);
                    double scale2 = Convert.ToDouble(props[3]);
                    bool firstAngle = Convert.ToDouble(props[4]) != 0;
                    double width = Convert.ToDouble(props[5]);
                    double height = Convert.ToDouble(props[6]);

                    bool success = swDraw.SetupSheet5(sheetName, paperSize, templateIn, scale1, scale2, firstAngle,
                        newTemplate, width, height, "", false);

                    if (!success)
                    {
                        summary.Warnings.Add($"Sheet '{sheetName}': failed to swap '{currentTemplate}' -> '{newTemplate}'.");
                        continue;
                    }

                    pendingConfirmations.Add((sheetName, currentTemplate, newTemplate));
                }
                catch (Exception ex)
                {
                    summary.Warnings.Add($"Sheet '{sheetName}': format replace threw an exception: {ex.Message}");
                }
            }

            swModel.EditRebuild3();

            if (pendingConfirmations.Count > 0)
            {
                SaveAndVerifyReload(swapp, swModel, filePath, pendingConfirmations, summary);
                return summary;
            }

            swapp.CloseDoc(swModel.GetTitle());
            return summary;
        }

        public SheetFormatSwapSummary SwapSheetFormatsBySize(string filePath, IReadOnlyDictionary<string, string> sheetSizeToNewTemplatePath)
        {
            string extension = System.IO.Path.GetExtension(filePath)?.ToLowerInvariant() ?? string.Empty;
            if (extension != ".slddrw")
            {
                throw new NotSupportedException($"File extension '{extension}' is not supported for sheet format swapping. Only .slddrw files are supported.");
            }

            SldWorks swapp = SolidworksSingleton.getApplication();
            ModelDoc2 swModel = swapp.OpenDoc6(filePath, (int)swDocumentTypes_e.swDocDRAWING, (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", 0, 0);

            if (swModel == null)
            {
                throw new InvalidOperationException($"Failed to open '{filePath}'.");
            }

            var summary = new SheetFormatSwapSummary();
            DrawingDoc swDraw = (DrawingDoc)swModel;

            string[] sheetNames = (string[])swDraw.GetSheetNames();
            var pendingConfirmations = new List<(string SheetName, string OldTemplate, string NewTemplate)>();

            foreach (string sheetName in sheetNames)
            {
                try
                {
                    if (!swDraw.ActivateSheet(sheetName))
                    {
                        summary.Warnings.Add($"Sheet '{sheetName}': could not activate, skipped.");
                        continue;
                    }

                    Sheet swSheet = (Sheet)swDraw.GetCurrentSheet();
                    string currentTemplate = swSheet.GetTemplateName();
                    double[] props = (double[])swSheet.GetProperties2();

                    int paperSize = Convert.ToInt32(props[0]);
                    string sheetSizeName = Enum.IsDefined(typeof(swDwgPaperSizes_e), paperSize)
                        ? ((swDwgPaperSizes_e)paperSize).ToString()
                        : paperSize.ToString();

                    if (!sheetSizeToNewTemplatePath.TryGetValue(sheetSizeName, out string? newTemplate))
                    {
                        summary.Warnings.Add($"Sheet '{sheetName}': size '{sheetSizeName}' not found in template map, skipped.");
                        continue;
                    }

                    int templateIn = Convert.ToInt32(props[1]);
                    double scale1 = Convert.ToDouble(props[2]);
                    double scale2 = Convert.ToDouble(props[3]);
                    bool firstAngle = Convert.ToDouble(props[4]) != 0;
                    double width = Convert.ToDouble(props[5]);
                    double height = Convert.ToDouble(props[6]);

                    bool success = swDraw.SetupSheet5(sheetName, paperSize, templateIn, scale1, scale2, firstAngle,
                        newTemplate, width, height, "", false);

                    if (!success)
                    {
                        summary.Warnings.Add($"Sheet '{sheetName}': failed to swap '{currentTemplate}' -> '{newTemplate}'.");
                        continue;
                    }

                    pendingConfirmations.Add((sheetName, currentTemplate, newTemplate));
                }
                catch (Exception ex)
                {
                    summary.Warnings.Add($"Sheet '{sheetName}': format replace threw an exception: {ex.Message}");
                }
            }

            swModel.EditRebuild3();

            if (pendingConfirmations.Count > 0)
            {
                SaveAndVerifyReload(swapp, swModel, filePath, pendingConfirmations, summary);
                return summary;
            }

            swapp.CloseDoc(swModel.GetTitle());
            return summary;
        }

        // Saves, closes, and reopens the drawing from disk so the confirmation reflects what was
        // actually persisted rather than the in-memory state (which can look correct even when the save didn't stick).
        private void SaveAndVerifyReload(SldWorks swapp, ModelDoc2 swModel, string filePath,
            List<(string SheetName, string OldTemplate, string NewTemplate)> pendingConfirmations, SheetFormatSwapSummary summary)
        {
            swModel.Save();
            swapp.CloseDoc(swModel.GetTitle());

            ModelDoc2 reloaded = swapp.OpenDoc6(filePath, (int)swDocumentTypes_e.swDocDRAWING, (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", 0, 0);

            if (reloaded == null)
            {
                foreach (var (sheetName, _, _) in pendingConfirmations)
                {
                    summary.Warnings.Add($"Sheet '{sheetName}': saved, but failed to reopen '{filePath}' to confirm the swap.");
                }

                return;
            }

            DrawingDoc reloadedDraw = (DrawingDoc)reloaded;
            bool reappliedAny = false;

            foreach (var (sheetName, oldTemplate, newTemplate) in pendingConfirmations)
            {
                try
                {
                    if (!reloadedDraw.ActivateSheet(sheetName))
                    {
                        summary.Warnings.Add($"Sheet '{sheetName}': reloaded, but could not re-activate to confirm the swap.");
                        continue;
                    }

                    Sheet reloadedSheet = (Sheet)reloadedDraw.GetCurrentSheet();
                    string verifiedTemplate = reloadedSheet.GetTemplateName();

                    bool confirmed = string.Equals(
                        System.IO.Path.GetFileName(verifiedTemplate),
                        System.IO.Path.GetFileName(newTemplate),
                        StringComparison.OrdinalIgnoreCase);

                    if (!confirmed)
                    {
                        summary.Warnings.Add($"Sheet '{sheetName}': expected format '{newTemplate}' after reload but found '{verifiedTemplate}'.");
                        continue;
                    }

                    // Equivalent to the manual "Reload Format" command: SetupSheet5 alone can leave stale/duplicate
                    // format geometry behind, so force the sheet to reload its format from the template file on disk.
                    // keepNoteChanges=false: discard any in-memory modifications and reload everything from the template.
                    var reloadResult = (swReloadTemplateResult_e)reloadedSheet.ReloadTemplate(false);

                    if (reloadResult != swReloadTemplateResult_e.swReloadTemplate_Success)
                    {
                        summary.Warnings.Add($"Sheet '{sheetName}': confirmed '{newTemplate}' but ReloadTemplate failed ({reloadResult}).");
                        continue;
                    }

                    reappliedAny = true;
                    summary.Swapped.Add($"Sheet '{sheetName}': '{oldTemplate}' -> '{newTemplate}' (confirmed after reload, template reloaded).");
                }
                catch (Exception ex)
                {
                    summary.Warnings.Add($"Sheet '{sheetName}': reload/confirm threw an exception: {ex.Message}");
                }
            }

            if (reappliedAny)
            {
                reloaded.EditRebuild3();
                reloaded.Save();
            }

            swapp.CloseDoc(reloaded.GetTitle());
        }

        public List<SheetFormatInfo> InspectSheetFormats(string filePath, List<string>? warnings = null)
        {
            string extension = System.IO.Path.GetExtension(filePath)?.ToLowerInvariant() ?? string.Empty;
            if (extension != ".slddrw")
            {
                throw new NotSupportedException($"File extension '{extension}' is not supported for sheet format inspection. Only .slddrw files are supported.");
            }

            SldWorks swapp = SolidworksSingleton.getApplication();
            ModelDoc2 swModel = swapp.OpenDoc6(filePath, (int)swDocumentTypes_e.swDocDRAWING, (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", 0, 0);

            if (swModel == null)
            {
                throw new InvalidOperationException($"Failed to open '{filePath}'.");
            }

            var results = new List<SheetFormatInfo>();
            DrawingDoc swDraw = (DrawingDoc)swModel;

            string[] sheetNames = (string[])swDraw.GetSheetNames();

            foreach (string sheetName in sheetNames)
            {
                if (!swDraw.ActivateSheet(sheetName))
                {
                    warnings?.Add($"Sheet '{sheetName}': could not activate, skipped.");
                    continue;
                }

                Sheet swSheet = (Sheet)swDraw.GetCurrentSheet();
                string currentTemplate = swSheet.GetTemplateName();
                double[] props = (double[])swSheet.GetProperties2();

                results.Add(new SheetFormatInfo
                {
                    SheetName = sheetName,
                    TemplatePath = currentTemplate,
                    TemplateFileName = System.IO.Path.GetFileName(currentTemplate),
                    PaperSize = Convert.ToInt32(props[0]),
                    TemplateIn = Convert.ToInt32(props[1]),
                    Scale1 = Convert.ToDouble(props[2]),
                    Scale2 = Convert.ToDouble(props[3]),
                    FirstAngle = Convert.ToDouble(props[4]) != 0,
                    Width = Convert.ToDouble(props[5]),
                    Height = Convert.ToDouble(props[6]),
                });
            }

            swapp.CloseDoc(swModel.GetTitle());
            return results;
        }

    }
}
