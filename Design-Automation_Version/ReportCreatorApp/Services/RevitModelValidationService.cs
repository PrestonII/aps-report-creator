using System;
using System.IO;

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;

using DesignAutomationFramework;

using ipx.revit.reports.Models;

namespace ipx.revit.reports.Services
{
    public static class RevitModelValidationService
    {
        private static Application? _rvtApp = null;
        private static Document? _doc = null;

        public static (Application, Document) GetRevitAssets()
        {
            if (_rvtApp == null) throw new Exception("The Revit Application is null!");
            if(_doc == null) throw new Exception("The Revit Document is null!");
            var result = (rvtApp: _rvtApp!, doc: _doc!);
            return result;
        }

        public static void ValidateDesignAutomationEnvironment(DesignAutomationData data, ProjectData projectData)
        {
            if (data == null)
            {
                LoggingService.LogError("DesignAutomationData is null.");
                throw new ArgumentNullException(nameof(data));
            }

            LoggingService.Log("Validating Design Automation Environment...");

            _rvtApp = data.RevitApp;
            if (_rvtApp == null)
            {
                LoggingService.LogError("RevitApp is null.");
                throw new InvalidDataException(nameof(_rvtApp));
            }

            string modelPath = data.FilePath;
            if (string.IsNullOrWhiteSpace(modelPath) && data.RevitDoc != null)
            {
                modelPath = data.RevitDoc.PathName;
                LoggingService.LogDebug($"Fallback: modelPath from RevitDoc.PathName: '{modelPath}'");
            }

            LoggingService.LogDebug($"Final modelPath: '{modelPath}'");

            if (String.IsNullOrWhiteSpace(modelPath))
            {
                LoggingService.LogError("modelPath is still null or whitespace after fallback.");
                throw new InvalidDataException(nameof(modelPath));
            }

            _doc = data.RevitDoc;
            if (_doc == null)
            {
                LoggingService.LogError("RevitDoc is null. Could not open document.");
                throw new InvalidOperationException("Could not open document.");
            }

            LoggingService.Log("Revit document opened successfully.");

            if (projectData == null)
            {
                LoggingService.LogError("Failed to parse input JSON.");
                throw new InvalidOperationException("Failed to parse input JSON.");
            }
            LoggingService.Log("Design Automation Environment validated!");
        }
    }
}
