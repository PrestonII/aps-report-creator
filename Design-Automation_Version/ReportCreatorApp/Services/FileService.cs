using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace ipx.revit.reports.Services
{
    public class FileService
    {
        private readonly string _username;
        private readonly string _password;
        private readonly HttpClient _httpClient;
        private readonly LoggingService _logger;

        public FileService(string username, string password, string environment = "development")
        {
            _username = username ?? throw new ArgumentNullException(nameof(username));
            _password = password ?? throw new ArgumentNullException(nameof(password));
            _httpClient = new HttpClient();
            _logger = new LoggingService(environment);
        }

        public async Task<string> DownloadFileAsync(string url, string localPath)
        {
            try
            {
                _logger.LogDebug($"Starting download from URL: {url} to local path: {localPath}");
                
                // Add basic authentication if needed
                if (!string.IsNullOrEmpty(_username) && !string.IsNullOrEmpty(_password))
                {
                    var authString = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{_username}:{_password}"));
                    _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authString);
                }

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                // Ensure the directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(localPath));

                // Save the file
                using (var fileStream = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await response.Content.CopyToAsync(fileStream);
                }

                // Ensure the file is in a Revit-compatible format
                localPath = EnsureRevitCompatibleImageFormat(localPath);

                _logger.Log($"File downloaded successfully to: {localPath}");
                return localPath;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to download file from {url}", ex);
                throw;
            }
        }

        /// <summary>
        /// Ensures the image is in a format compatible with Revit (BMP, JPG, JPEG, PNG, or TIFF)
        /// </summary>
        /// <param name="imagePath">The path to the image file</param>
        /// <returns>The path to the compatible image file</returns>
        private string EnsureRevitCompatibleImageFormat(string imagePath)
        {
            string extension = Path.GetExtension(imagePath).ToLowerInvariant();
            
            // Check if the file already has a compatible extension
            if (extension == ".bmp" || extension == ".jpg" || extension == ".jpeg" || 
                extension == ".png" || extension == ".tif" || extension == ".tiff")
            {
                return imagePath;
            }
            
            _logger.LogWarning($"Image format {extension} may not be compatible with Revit. Using as-is and relying on Revit's import capability.");
            
            // If we want to convert the image in the future, we could add conversion logic here
            // For now, we'll just return the original path and rely on Revit's import capabilities
            
            return imagePath;
        }

        public void DeleteFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _logger.LogDebug($"File deleted successfully: {filePath}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to delete file {filePath}", ex);
                throw;
            }
        }

        public void DeleteDirectory(string directoryPath)
        {
            try
            {
                if (Directory.Exists(directoryPath))
                {
                    Directory.Delete(directoryPath, true);
                    _logger.LogDebug($"Directory deleted successfully: {directoryPath}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to delete directory {directoryPath}", ex);
                throw;
            }
        }
    }
} 