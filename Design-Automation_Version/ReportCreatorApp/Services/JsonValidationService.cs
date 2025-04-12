using System;
using System.IO;
using Newtonsoft.Json;
using ipx.revit.reports.Models;

namespace ipx.revit.reports.Services
{
    /// <summary>
    /// Service for validating and parsing JSON input files
    /// </summary>
    public class JsonValidationService
    {
        private readonly LoggingService _logger;

        public JsonValidationService(string environment = "debug")
        {
            _logger = new LoggingService(environment);
        }

        /// <summary>
        /// Validates and parses the input JSON file
        /// </summary>
        /// <param name="jsonPath">Path to the JSON file</param>
        /// <returns>Parsed ProjectData object or null if validation fails</returns>
        public ProjectData ValidateAndParseProjectData(string jsonPath)
        {
            try
            {
                // Check if file exists
                if (!File.Exists(jsonPath))
                {
                    _logger.LogWarning($"Input JSON file not found at: {jsonPath}");
                    return null;
                }

                // Read and parse JSON content
                string jsonContent = File.ReadAllText(jsonPath);
                _logger.LogDebug($"Raw JSON content: {jsonContent}");

                ProjectData projectData = JsonConvert.DeserializeObject<ProjectData>(jsonContent);
                
                // Validate required fields
                if (projectData == null)
                {
                    _logger.LogError("Failed to deserialize JSON to ProjectData");
                    return null;
                }
                
                if (string.IsNullOrWhiteSpace(projectData.ProjectName))
                {
                    _logger.LogWarning("Project name is missing in the input JSON");
                }
                
                if (projectData.ViewTypes == null || projectData.ViewTypes.Count == 0)
                {
                    _logger.LogWarning("No view types specified in the input JSON");
                }
                
                if (projectData.ImageData == null || projectData.ImageData.Count == 0)
                {
                    _logger.LogWarning("No image data found in the input JSON");
                }
                
                // Log successful parsing
                _logger.Log($"Successfully parsed project data for project: {projectData.ProjectName}");
                _logger.Log($"Report type: {projectData.ReportType}");
                
                if (projectData.ViewTypes != null)
                {
                    _logger.Log($"Number of view types to export: {projectData.ViewTypes.Count}");
                }
                
                if (projectData.ImageData != null)
                {
                    _logger.Log($"Found {projectData.ImageData.Count} image assets in the input JSON");
                }

                if(projectData.Environment == null)
                {
                  _logger.LogWarning("Could not find an environment in the ProjectData - using DEBUG as the environment for this session");
                }
                
                return projectData;
            }
            catch (Exception ex)
            {
                _logger.LogError("Exception parsing JSON", ex);
                return null;
            }
        }
        
        /// <summary>
        /// Validates the authentication information in the project data
        /// </summary>
        /// <param name="projectData">The project data to validate</param>
        /// <returns>True if authentication is valid, false otherwise</returns>
        public bool ValidateAuthentication(ProjectData projectData)
        {
            if (projectData == null)
            {
                _logger.LogError("Project data is null");
                return false;
            }
            
            if (projectData.Authentication == null)
            {
                _logger.LogWarning("Authentication information is missing");
                return false;
            }
            
            string username = projectData.Authentication.Username;
            string password = projectData.Authentication.Password;
            
            if (string.IsNullOrEmpty(username))
            {
                _logger.LogWarning("Authentication username is missing");
                return false;
            }
            
            if (string.IsNullOrEmpty(password))
            {
                _logger.LogWarning("Authentication password is missing");
                return false;
            }
            
            _logger.Log($"Authentication information is valid for user: {username}");
            return true;
        }
    }
} 