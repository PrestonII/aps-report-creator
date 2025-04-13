using System;
using System.IO;

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;

using DesignAutomationFramework;

namespace ipx.revit.reports.Services
{
    public static class RevitModelValidationService
    {
        private static Application? _rvtApp = null;
        private static Document? _doc = null;

        public static (Application, Document) GetRevitAssets()
        {
            if (_rvtApp == null) throw new Exception("The Revit Application is null!");
            if (_doc == null) throw new Exception("The Revit Document is null!");
            var result = (rvtApp: _rvtApp!, doc: _doc!);
            return result;
        }

        public static void ValidateDesignAutomationEnvironment(DesignAutomationData data)
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
            LoggingService.Log("Design Automation Environment validated!");
        }

        public static string GetParamsJSONPath(DesignAutomationData data)
        {
            string revitFilePath = data.RevitDoc.PathName;

            // Extract the directory of the Revit file
            string revitFileDirectory = System.IO.Path.GetDirectoryName(revitFilePath);

            // Build the path to params.json
            string paramsJsonPath = System.IO.Path.Combine(revitFileDirectory, "params.json");

            var pathExists = File.Exists(paramsJsonPath);

            LoggingService.Log(pathExists ? $"Found the params data here: {paramsJsonPath}" : $"Could not find the params data here: {paramsJsonPath}");

            return paramsJsonPath;
        }

        public static string[] GetAssetsPaths(DesignAutomationData data)
        {
            string revitFilePath = data.RevitDoc.PathName;

            // Extract the directory of the Revit file
            string revitFileDirectory = System.IO.Path.GetDirectoryName(revitFilePath);

            // Build the path to params.json
            string assetsPath = System.IO.Path.Combine(revitFileDirectory, "assets");

            var assetsDirExists = Directory.Exists(assetsPath);

            if (assetsDirExists)
            {
                LoggingService.Log($"The asset files are here: {assetsPath}");
                string[] assetPaths = Directory.GetFiles(assetsPath);
                return assetPaths;
            }
            else
            {
                LoggingService.LogError($"The assets are not here {assetsPath}");
                throw new Exception(
                    $"The assets are not here {assetsPath}"
                );
            }
        }
    }
}
