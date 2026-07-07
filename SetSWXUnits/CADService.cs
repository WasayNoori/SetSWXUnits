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

            swModel.EditRebuild3();

            if (pendingConfirmations.Count > 0)
            {
                swModel.Save();

                foreach (var (sheetName, oldTemplate, newTemplate) in pendingConfirmations)
                {
                    if (!swDraw.ActivateSheet(sheetName))
                    {
                        summary.Warnings.Add($"Sheet '{sheetName}': saved, but could not re-activate to confirm the swap.");
                        continue;
                    }

                    Sheet swSheet = (Sheet)swDraw.GetCurrentSheet();
                    string verifiedTemplate = swSheet.GetTemplateName();

                    bool confirmed = string.Equals(
                        System.IO.Path.GetFileName(verifiedTemplate),
                        System.IO.Path.GetFileName(newTemplate),
                        StringComparison.OrdinalIgnoreCase);

                    if (confirmed)
                    {
                        summary.Swapped.Add($"Sheet '{sheetName}': '{oldTemplate}' -> '{newTemplate}' (confirmed after save).");
                    }
                    else
                    {
                        summary.Warnings.Add($"Sheet '{sheetName}': expected format '{newTemplate}' after save but found '{verifiedTemplate}'.");
                    }
                }
            }

            swapp.CloseDoc(swModel.GetTitle());
            return summary;
        }

        public List<SheetFormatInfo> InspectSheetFormats(string filePath)
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
