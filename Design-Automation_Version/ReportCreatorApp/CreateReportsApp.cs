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
      public ExternalDBApplicationResult OnStartup(ControlledApplication app)
      {
         DesignAutomationBridge.DesignAutomationReadyEvent += HandleDesignAutomationReadyEvent;
         return ExternalDBApplicationResult.Succeeded;
      }

      public ExternalDBApplicationResult OnShutdown(ControlledApplication app)
      {
         return ExternalDBApplicationResult.Succeeded;
      }

      public void HandleDesignAutomationReadyEvent(object sender, DesignAutomationReadyEventArgs e)
      {
         e.Succeeded = true;
         ExportToPdfs(e.DesignAutomationData);
      }

    public bool ExportToPdfs(DesignAutomationData data)
    {
        if (data == null)
        {
            Console.WriteLine("[ERROR] DesignAutomationData is null.");
            throw new ArgumentNullException(nameof(data));
        }

        Console.WriteLine("[INFO] Starting ExportToPdfs...");

        Application rvtApp = data.RevitApp;
        if (rvtApp == null)
        {
            Console.WriteLine("[ERROR] RevitApp is null.");
            throw new InvalidDataException(nameof(rvtApp));
        }

        string modelPath = data.FilePath;
        Console.WriteLine($"[DEBUG] modelPath from DesignAutomationData.FilePath: '{modelPath}'");

        if (String.IsNullOrWhiteSpace(modelPath))
        {
            Console.WriteLine("[ERROR] modelPath is null or whitespace.");
            throw new InvalidDataException(nameof(modelPath));
        }

        Document doc = data.RevitDoc;
        if (doc == null)
        {
            Console.WriteLine("[ERROR] RevitDoc is null. Could not open document.");
            throw new InvalidOperationException("Could not open document.");
        }

        Console.WriteLine("[INFO] Revit document opened successfully.");

        // Validate and parse the input JSON file
        JsonValidationService jsonValidationService = new JsonValidationService();
        ProjectData projectData = jsonValidationService.ValidateAndParseProjectData("params.json");
        
        if (projectData == null)
        {
            Console.WriteLine("[ERROR] Failed to parse input JSON.");
            throw new InvalidOperationException("Failed to parse input JSON.");
        }

        return ExportToPdfsImp(rvtApp, doc, projectData);
    }

    private bool ExportToPdfsImp(Application rvtApp, Document doc, ProjectData projectData)
    {
      using (Transaction tx = new Transaction(doc))
      {
        tx.Start("Export PDF");

        try
        {
            Console.WriteLine("[INFO] Starting report generation process...");

            // Get all views that match the specified view types
            List<View> views = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(vw => !vw.IsTemplate && vw.CanBePrinted && projectData.ViewTypes.Contains(vw.ViewType))
                .ToList();

            Console.WriteLine($"[INFO] Found {views.Count} views matching the specified view types");

            // Apply view filters if specified
            if (projectData.ViewFilters != null && projectData.ViewFilters.Count > 0)
            {
                Console.WriteLine($"[INFO] Applying {projectData.ViewFilters.Count} view filters");
                foreach (var filter in projectData.ViewFilters)
                {
                    Console.WriteLine($"[INFO] Applying filter: {filter.Name} ({filter.Type})");
                    // Apply filter logic here
                    // This would depend on the specific filter types and how they should be applied
                }
            }

            // Limit the number of views if specified
            if (projectData.MaxViews > 0 && views.Count > projectData.MaxViews)
            {
                Console.WriteLine($"[INFO] Limiting views from {views.Count} to {projectData.MaxViews}");
                views = views.Take(projectData.MaxViews).ToList();
            }

            // Get authentication credentials
            string username = projectData.Authentication?.Username;
            string password = projectData.Authentication?.Password;
            
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                Console.WriteLine("[WARNING] Authentication credentials not provided. Image downloading may fail.");
            }
            else
            {
                Console.WriteLine($"[INFO] Using provided authentication credentials for user: {username}");
            }
            
            // Create the report generation service
            ReportGenerationService reportService = new ReportGenerationService(doc, username, password);
            
            // Generate the image report
            List<ElementId> sheetIds = reportService.GenerateImageReport(projectData);
            
            // Export the sheets to PDF
            string outputFileName = projectData.OutputFileName ?? "AssetReport";
            reportService.ExportSheetsToPdf(sheetIds, outputFileName);
            
            Console.WriteLine("[INFO] Report generation process completed successfully");
            tx.Commit();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Exception during report generation: {ex.Message}");
            Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
            tx.RollBack();
            return false;
        }
      }
    }
  }
}
