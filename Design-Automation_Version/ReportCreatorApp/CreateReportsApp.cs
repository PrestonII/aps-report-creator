using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using DesignAutomationFramework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

        // Parse the input JSON file
        ProjectData projectData = ParseInputJson("params.json");
        if (projectData == null)
        {
            Console.WriteLine("[ERROR] Failed to parse input JSON.");
            throw new InvalidOperationException("Failed to parse input JSON.");
        }

        Console.WriteLine($"[INFO] Successfully parsed project data for project: {projectData.ProjectName}");
        Console.WriteLine($"[INFO] Report type: {projectData.ReportType}");
        Console.WriteLine($"[INFO] Number of view types to export: {projectData.ViewTypes.Count}");
        Console.WriteLine($"[INFO] Found {projectData.ImageData.Count} image assets in the input JSON");

        return ExportToPdfsImp(rvtApp, doc, projectData);
    }

    private ProjectData ParseInputJson(string jsonPath)
    {
        try
        {
            if (!File.Exists(jsonPath))
            {
                Console.WriteLine($"[WARNING] Input JSON file not found at: {jsonPath}");
                return null;
            }

            string jsonContent = File.ReadAllText(jsonPath);
            Console.WriteLine($"[DEBUG] Raw JSON content: {jsonContent}");

            ProjectData projectData = JsonConvert.DeserializeObject<ProjectData>(jsonContent);
            return projectData;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Exception parsing JSON: {ex.Message}");
            Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
            return null;
        }
    }

    private bool ExportToPdfsImp(Application rvtApp, Document doc, ProjectData projectData)
    {
      using (Transaction tx = new Transaction(doc))
      {
        tx.Start("Export PDF");

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

        // Create FileService with credentials from the project data
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
        
        FileService fileService = new FileService(username, password);
        
        // Create a temporary folder for downloaded images
        string tempFolder = Path.Combine(Path.GetTempPath(), "RevitImages");
        Directory.CreateDirectory(tempFolder);
        
        // Filter for image assets only
        var imageAssets = projectData.ImageData
            .Where(a => a.AssetType.Equals("image", StringComparison.OrdinalIgnoreCase))
            .ToList();
            
        Console.WriteLine($"[INFO] Found {imageAssets.Count} image assets to process");
        
        // TODO: Process assets and place images on sheets
        // This will be implemented in the next steps

        // Export the views to PDF
        if (views.Count > 0)
        {
            IList<ElementId> viewIds = views.Select(v => v.Id).ToList();
            
            PDFExportOptions options = new PDFExportOptions();
            options.FileName = projectData.OutputFileName ?? "result";
            options.Combine = true;
            
            string workingFolder = Directory.GetCurrentDirectory();
            Console.WriteLine($"[INFO] Exporting {viewIds.Count} views to PDF in folder: {workingFolder}");
            
            doc.Export(workingFolder, viewIds, options);
            Console.WriteLine($"[INFO] PDF export completed successfully");
        }
        else
        {
            Console.WriteLine("[WARNING] No views found matching the specified criteria");
        }
        
        tx.RollBack();
      }
      return true;
    }
  }
}
