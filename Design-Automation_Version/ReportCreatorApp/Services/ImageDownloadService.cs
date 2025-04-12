using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

namespace ipx.revit.reports.Services
{
    public sealed class ImageDownloadService
    {
        private static readonly Lazy<ImageDownloadService> _instance = new(() => new ImageDownloadService());
        public static ImageDownloadService Instance => _instance.Value;

        private readonly HttpClient _httpClient;

        private ImageDownloadService()
        {
            _httpClient = new HttpClient();
        }

        public async Task<List<string>> DownloadAndExtractImagesAsync(string zipUrl, string outputDir, Dictionary<string, string>? headers = null)
        {
            if (string.IsNullOrWhiteSpace(zipUrl))
                throw new ArgumentException("Zip URL must not be null or empty.");

            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            string tempZipPath = Path.Combine(Path.GetTempPath(), $"images_{Guid.NewGuid()}.zip");

            try
            {
                // Clear and set custom headers
                _httpClient.DefaultRequestHeaders.Clear();
                if (headers != null)
                {
                    foreach (var kvp in headers)
                        _httpClient.DefaultRequestHeaders.Add(kvp.Key, kvp.Value);
                }

                // Download zip
                using (var response = await _httpClient.GetAsync(zipUrl))
                {
                    response.EnsureSuccessStatusCode();
                    using FileStream fs = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write);
                    await response.Content.CopyToAsync(fs);

                }

                // Extract contents
                ZipFile.ExtractToDirectory(tempZipPath, outputDir);

                // Return list of image file paths
                string[] extensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff" };
                List<string> imageFiles = new();

                foreach (var file in Directory.GetFiles(outputDir, "*.*", SearchOption.AllDirectories))
                {
                    if (Array.Exists(extensions, ext => file.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                        imageFiles.Add(file);
                }

                return imageFiles;
            }
            finally
            {
                if (File.Exists(tempZipPath))
                    File.Delete(tempZipPath);
            }
        }
    }
}
