using System;
using System.IO;

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
                var data = ProjectDataValidationService.ValidateProjectData("params.json");
                LoggingService.SetEnvironment(data.Environment);
                ExportToPdfs(e.DesignAutomationData, data);
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

        public void ExportToPdfs(DesignAutomationData designAutomationData, ProjectData projectData)
        {
            RevitModelValidationService.ValidateDesignAutomationEnvironment(designAutomationData, projectData);
            var (rvtApp, doc) = RevitModelValidationService.GetRevitAssets();

            LoggingService.Log(Directory.Exists("assets.zip") ? "The assets file is already here!" : "The assets file needs to be downloaded");

            ExportToPdfsImp(rvtApp, doc, projectData);
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
