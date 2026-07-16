
//------------------------------------------------------------
// Preconditions:
// 1. Make a copy of:
//    C:\ProgramData\SolidWorks\SOLIDWORKS version\lang\english\sheetformat\a0 - iso.slddrt.
// 2. Create a new blank drawing using standard sheet size AO (ISO).
// 3. Add another blank sheet to the drawing, for a total of two sheets.
// 4. Open the Immediate window.
//
// Postconditions:
// 1. Modifies the sheet format template to include a new
//    note.
// 2. Examine Sheet1, Sheet2, and the Immediate window.
// 3. Delete:
//    C:\ProgramData\SolidWorks\SOLIDWORKS version\lang\english\sheetformat\a0 - iso.slddrt.
// 4. Rename the copy that you made in Preconditions step 1 to:
//    C:\ProgramData\SolidWorks\SOLIDWORKS version\lang\english\sheetformat\a0 - iso.slddrt.
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System.Runtime.InteropServices;
using System;
using System.Diagnostics;
 
namespace Macro1CSharp.csproj
{
    public partial class SolidWorksMacro
    {
 
        const bool TEST_APPLY_CHANGES_TO_ALL = true;
 
        private string GetReloadResult(swReloadTemplateResult_e result)
        {
            string functionReturnValue = null;
            switch (result)
            {
                case (swReloadTemplateResult_e)swReloadTemplateResult_e.swReloadTemplate_Success:
                    functionReturnValue = "Success";
                    break;
                case (swReloadTemplateResult_e)swReloadTemplateResult_e.swReloadTemplate_UnknownError:
                    functionReturnValue = "FAIL - Unknown Error";
                    break;
                case (swReloadTemplateResult_e)swReloadTemplateResult_e.swReloadTemplate_FileNotFound:
                    functionReturnValue = "FAIL - File Not Found";
                    break;
                case (swReloadTemplateResult_e)swReloadTemplateResult_e.swReloadTemplate_CustomSheet:
                    functionReturnValue = "FAIL - Custom Sheet";
                    break;
                case (swReloadTemplateResult_e)swReloadTemplateResult_e.swReloadTemplate_ViewOnly:
                    functionReturnValue = "FAIL - View Only";
                    break;
                default:
                    functionReturnValue = "FAIL - <unrecognized error code - " + result + ">";
                    break;
            }
            return functionReturnValue;
        }
 
        public void Main()
        {
 
            ModelDoc2 swModel = default(ModelDoc2);
            swModel = (ModelDoc2)swApp.ActiveDoc;
 
            if (swModel == null)
            {
                Debug.Print("Create a new empty drawing and add a second sheet to the drawing.");
                return;
            }
 
            if (swModel.GetType() != (int)swDocumentTypes_e.swDocDRAWING)
                return;
 
            DrawingDoc swDrwng = default(DrawingDoc);
            swDrwng = (DrawingDoc)swModel;
 
            //Get the current sheet
            Sheet activeSheet = default(Sheet);
            activeSheet = (Sheet)swDrwng.GetCurrentSheet();
            Debug.Print("Active sheet name: " + activeSheet.GetName());
 
            //Get the sheet format template
            string templateName = null;
            templateName = activeSheet.GetTemplateName();
            Debug.Print("Sheet format template name to modify: " + templateName);
            swDrwng.EditTemplate();
 
            //Add a new note to the sheet format template
            Note swNote = default(Note);
            swNote = (Note)swModel.InsertNote("A New Note");
            Annotation swAnno = default(Annotation);
            swAnno = (Annotation)swNote.GetAnnotation();
            swAnno.SetPosition2(0, 0.2, 0);
            TextFormat txtFormat = default(TextFormat);
            txtFormat = (TextFormat)swAnno.GetTextFormat(0);
            txtFormat.BackWards = (txtFormat.BackWards == false);
            txtFormat.Bold = true;
            txtFormat.CharHeightInPts = 10 * txtFormat.CharHeightInPts;
            swAnno.SetTextFormat(0, false, txtFormat);
            swDrwng.EditSheet();
 
            //At this point, the active sheet's format has changed    
 
            if (TEST_APPLY_CHANGES_TO_ALL)
            {
                //Save sheet format back to original sheet format template
                activeSheet.SaveFormat(templateName);
                //Reload all other sheets from the updated sheet format template
                object[] vSheetNames = null;
                vSheetNames = (object[])swDrwng.GetSheetNames();
                foreach (string vName in vSheetNames)
                {
                    if (vName != (string)activeSheet.GetName())
                    {
                        Debug.Print("Other sheet name: " + vName);
                        Sheet otherSheet = default(Sheet);
                        otherSheet = (Sheet)swDrwng.get_Sheet(vName);
                        if (otherSheet.GetTemplateName() == templateName)
                        {
                            swReloadTemplateResult_e reloadResult = default(swReloadTemplateResult_e);
 
                            //Keep modifications and and reload all other elements
                            //from original sheet format template
                            reloadResult = (swReloadTemplateResult_e)otherSheet.ReloadTemplate(true);
 
                            //Discard modifications and reload all elements from
                            //original sheet format template
                            //reloadResult = otherSheet.ReloadTemplate(False) 
 
                            Debug.Print("Reload sheet format for <" + otherSheet.GetName() + ">: " + GetReloadResult(reloadResult));
 
                        }
                    }
                }
                swDrwng.ActivateSheet(activeSheet.GetName());
 
            }
            else
            {
 
                //Discard the changes and reload the original sheet format template
                swReloadTemplateResult_e reloadResult = default(swReloadTemplateResult_e);
                reloadResult = (swReloadTemplateResult_e)activeSheet.ReloadTemplate(false);
                Debug.Print("Done - " + GetReloadResult(reloadResult));
 
            }
 
        }
 
        /// <summary>
        ///  The SldWorks swApp variable is pre-assigned for you.
        /// </summary>
        public SldWorks swApp;
    }
}