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

        public static void ValidateDesignAutomationEnvironment(DesignAutomationData data, LoggingService _logger, ProjectData projectData)
        {
            if (data == null)
            {
                _logger.LogError("DesignAutomationData is null.");
                throw new ArgumentNullException(nameof(data));
            }

            _logger.Log("Validating Design Automation Environment...");

            _rvtApp = data.RevitApp;
            if (_rvtApp == null)
            {
                _logger.LogError("RevitApp is null.");
                throw new InvalidDataException(nameof(_rvtApp));
            }

            string modelPath = data.FilePath;
            if (string.IsNullOrWhiteSpace(modelPath) && data.RevitDoc != null)
            {
                modelPath = data.RevitDoc.PathName;
                _logger.LogDebug($"Fallback: modelPath from RevitDoc.PathName: '{modelPath}'");
            }

            _logger.LogDebug($"Final modelPath: '{modelPath}'");

            if (String.IsNullOrWhiteSpace(modelPath))
            {
                _logger.LogError("modelPath is still null or whitespace after fallback.");
                throw new InvalidDataException(nameof(modelPath));
            }

            _doc = data.RevitDoc;
            if (_doc == null)
            {
                _logger.LogError("RevitDoc is null. Could not open document.");
                throw new InvalidOperationException("Could not open document.");
            }

            _logger.Log("Revit document opened successfully.");

            if (projectData == null)
            {
                _logger.LogError("Failed to parse input JSON.");
                throw new InvalidOperationException("Failed to parse input JSON.");
            }
            _logger.Log("Design Automation Environment validated!");
        }
    }
}
