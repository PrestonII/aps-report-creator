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
        private LoggingService _logger = new();
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
                Console.WriteLine("IPX LOGGER: DesignAutomationReadyEvent: Starting event handling");
                var data = ProjectDataValidationService.ValidateProjectData();
                _logger = new LoggingService(data.Environment);
                ExportToPdfs(e.DesignAutomationData, data);
                e.Succeeded = _success ?? false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                e.Succeeded = _success ?? false;
            }
            finally
            {
                _logger.Log("IPX LOGGER: DesignAutomationReadyEvent: Ending event handling");
            }
        }

        public void ExportToPdfs(DesignAutomationData designAutomationData, ProjectData projectData)
        {
            RevitModelValidationService.ValidateDesignAutomationEnvironment(designAutomationData, _logger, projectData);
            var (rvtApp, doc) = RevitModelValidationService.GetRevitAssets();

            _logger.Log(Directory.Exists("assets.zip") ? "The assets file is already here!" : "The assets file needs to be downloaded");

            ExportToPdfsImp(rvtApp, doc, projectData);
        }

        private void ExportToPdfsImp(Application rvtApp, Document doc, ProjectData projectData)
        {
            using (Transaction tx = new(doc))
            {
                tx.Start("Export PDF");

                try
                {
                    _logger.Log("Starting report generation process...");

                    // Create the report generation service
                    string username = projectData.Authentication?.Username ?? "";
                    string password = projectData.Authentication?.Password ?? "";
                    ReportGenerationService reportService = new ReportGenerationService(doc, username, password, projectData.Environment);

                    List<ElementId> sheetIds = new List<ElementId>();

                    // Check if we have image data to place on individual sheets
                    if (projectData.ImageData != null && projectData.ImageData.Count > 0)
                    {
                        _logger.Log($"Processing {projectData.ImageData.Count} images for individual sheets");
                        // Call the new method to place images on individual sheets
                        sheetIds = reportService.PlaceImagesOnIndividualSheets(projectData);
                    }
                    else
                    {
                        // Get all views that match the specified view types
                        List<View> views = new FilteredElementCollector(doc)
                            .OfClass(typeof(View))
                            .Cast<View>()
                            .Where(vw => !vw.IsTemplate && vw.CanBePrinted && projectData.ViewTypes.Contains(vw.ViewType.ToString()))
                            .ToList();

                        _logger.Log($"Found {views.Count} views matching the specified view types");

                        // Apply view filters if specified
                        if (projectData.ViewFilters != null && projectData.ViewFilters.Count > 0)
                        {
                            _logger.Log($"Applying {projectData.ViewFilters.Count} view filters");
                            foreach (var filter in projectData.ViewFilters)
                            {
                                _logger.Log($"Applying filter: {filter.Name} ({filter.Type})");
                                // Apply filter logic here
                                // This would depend on the specific filter types and how they should be applied
                            }
                        }

                        // Limit the number of views if specified
                        if (projectData.MaxViews > 0 && views.Count > projectData.MaxViews)
                        {
                            _logger.Log($"Limiting views from {views.Count} to {projectData.MaxViews}");
                            views = views.Take(projectData.MaxViews).ToList();
                        }

                        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                        {
                            _logger.LogWarning("Authentication credentials not provided. Image downloading may fail.");
                        }
                        else
                        {
                            _logger.Log($"Using provided authentication credentials for user: {username}");
                        }

                        _logger.Log("Report generation process completed successfully");
                        tx.Commit();
                    }
                    _success = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError("Exception during report generation", ex);
                    tx.RollBack();
                    _success = false;
                }
            }
        }
    }
}
