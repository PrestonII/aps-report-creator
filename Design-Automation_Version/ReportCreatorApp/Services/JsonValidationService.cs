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
                    Console.WriteLine($"[WARNING] Input JSON file not found at: {jsonPath}");
                    return null;
                }

                // Read and parse JSON content
                string jsonContent = File.ReadAllText(jsonPath);
                Console.WriteLine($"[DEBUG] Raw JSON content: {jsonContent}");

                ProjectData projectData = JsonConvert.DeserializeObject<ProjectData>(jsonContent);
                
                // Validate required fields
                if (projectData == null)
                {
                    Console.WriteLine("[ERROR] Failed to deserialize JSON to ProjectData");
                    return null;
                }
                
                if (string.IsNullOrWhiteSpace(projectData.ProjectName))
                {
                    Console.WriteLine("[WARNING] Project name is missing in the input JSON");
                }
                
                if (projectData.ViewTypes == null || projectData.ViewTypes.Count == 0)
                {
                    Console.WriteLine("[WARNING] No view types specified in the input JSON");
                }
                
                if (projectData.ImageData == null || projectData.ImageData.Count == 0)
                {
                    Console.WriteLine("[WARNING] No image data found in the input JSON");
                }
                
                // Log successful parsing
                Console.WriteLine($"[INFO] Successfully parsed project data for project: {projectData.ProjectName}");
                Console.WriteLine($"[INFO] Report type: {projectData.ReportType}");
                
                if (projectData.ViewTypes != null)
                {
                    Console.WriteLine($"[INFO] Number of view types to export: {projectData.ViewTypes.Count}");
                }
                
                if (projectData.ImageData != null)
                {
                    Console.WriteLine($"[INFO] Found {projectData.ImageData.Count} image assets in the input JSON");
                }
                
                return projectData;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Exception parsing JSON: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
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
                Console.WriteLine("[ERROR] Project data is null");
                return false;
            }
            
            if (projectData.Authentication == null)
            {
                Console.WriteLine("[WARNING] Authentication information is missing");
                return false;
            }
            
            string username = projectData.Authentication.Username;
            string password = projectData.Authentication.Password;
            
            if (string.IsNullOrEmpty(username))
            {
                Console.WriteLine("[WARNING] Authentication username is missing");
                return false;
            }
            
            if (string.IsNullOrEmpty(password))
            {
                Console.WriteLine("[WARNING] Authentication password is missing");
                return false;
            }
            
            Console.WriteLine($"[INFO] Authentication information is valid for user: {username}");
            return true;
        }
    }
} 