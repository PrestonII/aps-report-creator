using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;

using DesignAutomationFramework;

using ipx.revit.reports.Models;
using ipx.revit.reports.Services;

namespace ipx.revit.reports
{
    [Autodesk.Revit.Attributes.Regeneration(Autodesk.Revit.Attributes.RegenerationOption.Manual)]
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class CreateReportsApp : IExternalDBApplication
    {
        private bool? _success = null;

        public ExternalDBApplicationResult OnStartup(ControlledApplication app)
        {
            Console.WriteLine("WELCOME FROM IPX - WE'RE LIVE AND LOGGING!");
            DesignAutomationBridge.DesignAutomationReadyEvent += HandleDesignAutomationReadyEvent;
            return ExternalDBApplicationResult.Succeeded;
        }

        public ExternalDBApplicationResult OnShutdown(ControlledApplication app)
        {
            return ExternalDBApplicationResult.Succeeded;
        }

        public void HandleDesignAutomationReadyEvent(object sender, DesignAutomationReadyEventArgs e)
        {
            try
            {
                LoggingService.LogDebug("IPX LOGGER: DesignAutomationReadyEvent: Starting event handling");

                // Step 1: Validate the Design Automation environment and get the host file
                RevitModelValidationService.ValidateDesignAutomationEnvironment(e.DesignAutomationData);

                // Get the host document
                var (rvtApp, doc) = RevitModelValidationService.GetRevitAssets();

                // Step 1: Link reference files
                int linkedCount = RevitModelValidationService.LinkReferenceFiles(doc);
                LoggingService.Log($"Linked {linkedCount} reference files");

                // Step 2: Create levels and views based on the linked files
                var views = RevitViewService.CreateLevelsAndViews(doc);
                LoggingService.Log($"Created {views.Count} views");

                // Create sheets and place views on them
                int sheetCount = RevitSheetService.CreateAndPlaceViewsOnSheets(doc, views);
                LoggingService.Log($"Created and placed views on {sheetCount} sheets");

                // Get additional data
                var assetPaths = RevitModelValidationService.GetAssetsPaths(e.DesignAutomationData);
                var paramsJsonPath = RevitModelValidationService.GetParamsJSONPath(e.DesignAutomationData);
                var projectData = ProjectDataValidationService.ValidateProjectData(paramsJsonPath);

                LoggingService.SetEnvironment(projectData.Environment);

                // Continue with the rest of the process
                ExportToPdfs(e.DesignAutomationData, projectData, assetPaths);
                e.Succeeded = _success ?? false;
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex.Message);
                e.Succeeded = _success ?? false;
            }
            finally
            {
                LoggingService.Log("IPX LOGGER: DesignAutomationReadyEvent: Ending event handling");
            }
        }

        public void ExportToPdfs(DesignAutomationData designAutomationData, ProjectData projectData, string[] assetPaths)
        {
            var (rvtApp, doc) = RevitModelValidationService.GetRevitAssets();

            // Import any additional assets if needed
            //if (assetPaths != null && assetPaths.Length > 0)
            //{
            //    ImportImageAssets(doc, assetPaths);
            //}

            ExportToPdfsImp(doc, projectData);
        }

        private void ImportImageAssets(Document doc, string[] images)
        {
            int viewCount = 1;
            try
            {
                using Transaction tr = new(doc, "Starting image importing...");
                tr.Start();

                foreach (var image in images)
                {
                    viewCount += 1;

                    LoggingService.LogDebug($"Starting placement of view #{viewCount}");
                    var viewName = $"Drafting View {viewCount}";
                    var view = RevitImageService.CreateDraftingView(doc, viewName);
                    var imageType = RevitImageService.ImportImage(doc, image);
                    var imageInstance = RevitImageService.PlaceImageOnView(
                        doc,
                        imageType,
                        view
                    );
                    LoggingService.LogDebug($"Placed image #{viewCount} on view ${viewName}");
                }
                tr.Commit();
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Could not handle image importing");
                LoggingService.LogError(ex.Message);
            }
        }

        private void ExportToPdfsImp(Document doc, ProjectData projectData)
        {
            using Transaction tx = new(doc);
            tx.Start("Export PDF");

            try
            {
                LoggingService.Log("Starting report generation process...");

                // Get all views that match the specified view types
                List<View> views = new FilteredElementCollector(doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Where(vw => !vw.IsTemplate && vw.CanBePrinted && vw.ViewType == ViewType.DrawingSheet)
                    .ToList();

                // Export the views to PDF
                if (views.Count > 0)
                {
                    IList<ElementId> viewIds = views.Select(v => v.Id).ToList();

                    PDFExportOptions options = new PDFExportOptions
                    {
                        FileName = projectData.OutputFileName ?? "result",
                        Combine = true
                    };

                    string workingFolder = Directory.GetCurrentDirectory();
                    Console.WriteLine($"[INFO] Exporting {viewIds.Count} views to PDF in folder: {workingFolder}");

                    doc.Export(workingFolder, viewIds, options);
                    Console.WriteLine($"[INFO] PDF export completed successfully");
                }
                else
                {
                    Console.WriteLine("[WARNING] No views found matching the specified criteria");
                }

                tx.Commit();
                LoggingService.Log("Report generation successfully completed...");
                _success = true;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Exception during report generation", ex);
                tx.RollBack();
                _success = false;
            }
        }
    }
}
