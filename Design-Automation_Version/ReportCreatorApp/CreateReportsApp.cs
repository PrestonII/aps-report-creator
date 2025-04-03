﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
      private LoggingService _logger = null!;

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
         ExportToPdfs(e.DesignAutomationData).Wait();
      }

    public async Task<bool> ExportToPdfs(DesignAutomationData data)
    {
        if (data == null)
        {
            _logger.LogError("DesignAutomationData is null.");
            throw new ArgumentNullException(nameof(data));
        }

        _logger.Log("Starting ExportToPdfs...");

        Application rvtApp = data.RevitApp;
        if (rvtApp == null)
        {
            _logger.LogError("RevitApp is null.");
            throw new InvalidDataException(nameof(rvtApp));
        }

        string modelPath = data.FilePath;
        _logger.LogDebug($"modelPath from DesignAutomationData.FilePath: '{modelPath}'");

        if (String.IsNullOrWhiteSpace(modelPath))
        {
            _logger.LogError("modelPath is null or whitespace.");
            throw new InvalidDataException(nameof(modelPath));
        }

        Document doc = data.RevitDoc;
        if (doc == null)
        {
            _logger.LogError("RevitDoc is null. Could not open document.");
            throw new InvalidOperationException("Could not open document.");
        }

        _logger.Log("Revit document opened successfully.");

        // Validate and parse the input JSON file
        JsonValidationService jsonValidationService = new JsonValidationService();
        ProjectData projectData = jsonValidationService.ValidateAndParseProjectData("params.json");
        
        if (projectData == null)
        {
            _logger.LogError("Failed to parse input JSON.");
            throw new InvalidOperationException("Failed to parse input JSON.");
        }

        // Initialize logger with environment setting
        _logger = new LoggingService(projectData.Environment);

        return await ExportToPdfsImp(rvtApp, doc, projectData);
    }

    private async Task<bool> ExportToPdfsImp(Application rvtApp, Document doc, ProjectData projectData)
    {
      using (Transaction tx = new Transaction(doc))
      {
        tx.Start("Export PDF");

        try
        {
            _logger.Log("Starting report generation process...");

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

            // Get authentication credentials
            string username = projectData.Authentication?.Username;
            string password = projectData.Authentication?.Password;
            
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                _logger.LogWarning("Authentication credentials not provided. Image downloading may fail.");
            }
            else
            {
                _logger.Log($"Using provided authentication credentials for user: {username}");
            }
            
            // Create the report generation service
            ReportGenerationService reportService = new ReportGenerationService(doc, username, password);
            
            // Generate the image report
            List<ElementId> sheetIds = await reportService.GenerateImageReport(projectData);
            
            // Export the sheets to PDF
            string outputFileName = projectData.OutputFileName ?? "AssetReport";
            reportService.ExportSheetsToPdf(sheetIds, outputFileName);
            
            _logger.Log("Report generation process completed successfully");
            tx.Commit();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("Exception during report generation", ex);
            tx.RollBack();
            return false;
        }
      }
    }
  }
}
