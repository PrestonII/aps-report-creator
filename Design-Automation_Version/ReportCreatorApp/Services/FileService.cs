using System;
using System.IO;
using System.Net;
using System.Net.Http;

namespace ipx.revit.reports.Services
{
    public class FileService
    {
        private readonly string _username;
        private readonly string _password;
        private readonly LoggingService _logger;
        private readonly HttpClient _httpClient;

        public FileService(string username, string password, string environment = "development")
        {
            _username = username ?? throw new ArgumentNullException(nameof(username));
            _password = password ?? throw new ArgumentNullException(nameof(password));
            _logger = new LoggingService(environment);

            ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true; // ⚠️ Trust all certs for debug only

            var handler = new HttpClientHandler { AllowAutoRedirect = false };
            _httpClient = new HttpClient(handler);
        }

        public string DownloadFile(string url, string localPath)
        {
            try
            {
                _logger.LogDebug($"\uD83D\uDCC5 Starting download from URL: {url} to local path: {localPath}");

                string currentUrl = url;
                int redirectCount = 0;
                const int MaxRedirects = 10;
                HttpResponseMessage response = null;

                while (redirectCount < MaxRedirects)
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, currentUrl);

                    request.Headers.UserAgent.ParseAdd("PostmanRuntime/7.43.3");
                    request.Headers.Accept.ParseAdd("*/*");
                    request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };

                    if (currentUrl.StartsWith("https://file.ipx-app.com"))
                    {
                        var authValue = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{_username}:{_password}"));
                        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);
                        _logger.LogDebug($"✅ Added Basic Auth to: {currentUrl}");
                    }
                    else
                    {
                        _logger.LogDebug($"🚫 No auth added to: {currentUrl}");
                    }

                    _logger.LogDebug($"➡️ Sending request to: {currentUrl}");
                    response = _httpClient.SendAsync(request).Result;

                    _logger.LogDebug($"↩️ Status: {(int)response.StatusCode} {response.StatusCode}");
                    _logger.LogDebug("📦 Response headers:");
                    foreach (var header in response.Headers)
                        _logger.LogDebug($"    {header.Key}: {string.Join(", ", header.Value)}");

                    if (response.StatusCode == HttpStatusCode.MovedPermanently ||
                        response.StatusCode == HttpStatusCode.Found ||
                        response.StatusCode == HttpStatusCode.TemporaryRedirect)
                    {
                        if (response.Headers.Location == null)
                        {
                            _logger.LogWarning("Redirect received but no Location header found.");
                            throw new InvalidOperationException("Redirect received with no Location header.");
                        }

                        string redirectUrl = response.Headers.Location.IsAbsoluteUri
                            ? response.Headers.Location.AbsoluteUri
                            : new Uri(new Uri(currentUrl), response.Headers.Location).AbsoluteUri;

                        _logger.LogDebug($"🔁 Redirecting to: {redirectUrl}");
                        currentUrl = redirectUrl;
                        redirectCount++;
                        continue;
                    }

                    break;
                }

                if (response == null)
                    throw new Exception("No response received.");

                response.EnsureSuccessStatusCode();

                Directory.CreateDirectory(Path.GetDirectoryName(localPath));
                using var fs = new FileStream(localPath, FileMode.Create, FileAccess.Write);
                response.Content.CopyToAsync(fs).Wait();

                _logger.Log($"✅ Downloaded file to: {localPath}");
                return EnsureRevitCompatibleImageFormat(localPath);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Failed to download file from {url}", ex);
                throw;
            }
        }

        private string EnsureRevitCompatibleImageFormat(string imagePath)
        {
            string extension = Path.GetExtension(imagePath).ToLowerInvariant();

            if (extension == ".bmp" || extension == ".jpg" || extension == ".jpeg" ||
                extension == ".png" || extension == ".tif" || extension == ".tiff")
            {
                return imagePath;
            }

            _logger.LogWarning($"⚠️ Image format {extension} may not be compatible with Revit. Using as-is.");
            return imagePath;
        }

        public void DeleteFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _logger.LogDebug($"🗑️ File deleted: {filePath}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Failed to delete file {filePath}", ex);
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
                    _logger.LogDebug($"🧹 Directory deleted: {directoryPath}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Failed to delete directory {directoryPath}", ex);
                throw;
            }
        }
    }
}