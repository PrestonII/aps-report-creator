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

        public FileService(string username, string password)
        {
            _username = username ?? throw new ArgumentNullException(nameof(username));
            _password = password ?? throw new ArgumentNullException(nameof(password));
            _httpClient = new HttpClient();
        }

        public async Task<string> DownloadFileAsync(string url, string localPath)
        {
            try
            {
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

                return localPath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to download file from {url}: {ex.Message}");
                throw;
            }
        }

        public void DeleteFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to delete file {filePath}: {ex.Message}");
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
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to delete directory {directoryPath}: {ex.Message}");
                throw;
            }
        }
    }
} 