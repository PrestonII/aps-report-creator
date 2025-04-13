using System;
using System.IO;

using ipx.revit.reports.Models;

using Newtonsoft.Json;

namespace ipx.revit.reports.Services
{
    public static class ProjectDataValidationService
    {
        /// <summary>
        /// Validates and parses the input JSON file
        /// </summary>
        /// <param name="jsonPath">Path to the JSON file</param>
        /// <returns>Parsed ProjectData object or null if validation fails</returns>
        public static ProjectData ValidateProjectData(string jsonPath)
        {
            try
            {
                // Check if file exists
                if (!File.Exists(jsonPath))
                {
                    LoggingService.LogError($"Input JSON file not found at: {jsonPath}");
                    throw new Exception($"Input JSON file not found at: {jsonPath}");
                }

                // Read and parse JSON content
                string jsonContent = File.ReadAllText(jsonPath);
                LoggingService.LogDebug($"Raw JSON content: {jsonContent}");

                ProjectData? projectData = JsonConvert.DeserializeObject<ProjectData>(jsonContent);

                // Validate required fields
                if (projectData == null)
                {
                    LoggingService.LogError("Failed to deserialize JSON to ProjectData");
                    throw new Exception("Failed to deserialize JSON to Project Data");
                }

                if (string.IsNullOrWhiteSpace(projectData.ProjectName))
                {
                    LoggingService.LogWarning("Project name is missing in the input JSON");
                }

                if (projectData.ViewTypes == null || projectData.ViewTypes.Count == 0)
                {
                    LoggingService.LogWarning("No view types specified in the input JSON");
                }

                if (projectData.ImageData == null || projectData.ImageData.Count == 0)
                {
                    LoggingService.LogWarning("No image data found in the input JSON");
                }

                // Log successful parsing
                LoggingService.Log($"Successfully parsed project data for project: {projectData.ProjectName}");
                LoggingService.Log($"Report type: {projectData.ReportType}");

                if (projectData.ViewTypes != null)
                {
                    LoggingService.Log($"Number of view types to export: {projectData.ViewTypes.Count}");
                }

                if (projectData.ImageData != null)
                {
                    LoggingService.Log($"Found {projectData.ImageData.Count} image assets in the input JSON");
                }

                if (projectData.Environment == null)
                {
                    LoggingService.LogWarning("Could not find an environment in the ProjectData - using DEBUG as the environment for this session");
                }

                return projectData;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Exception parsing JSON", ex);
                throw ex;
            }
        }
    }
}
