﻿using System;

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
                RevitModelValidationService.ValidateDesignAutomationEnvironment(e.DesignAutomationData);
                var assetPaths = RevitModelValidationService.GetAssetsPaths(e.DesignAutomationData);
                var paramsJsonPath = RevitModelValidationService.GetParamsJSONPath(e.DesignAutomationData);
                var projectData = ProjectDataValidationService.ValidateProjectData(paramsJsonPath);

                LoggingService.SetEnvironment(projectData.Environment);
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

            ImportImageAssets(doc, assetPaths);

            ExportToPdfsImp(rvtApp, doc, projectData);
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

        private void ExportToPdfsImp(Application rvtApp, Document doc, ProjectData projectData)
        {
            using Transaction tx = new(doc);
            tx.Start("Export PDF");

            try
            {
                LoggingService.Log("Starting report generation process...");

                tx.Commit();
                LoggingService.Log("Report generation successfully completed...");
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
