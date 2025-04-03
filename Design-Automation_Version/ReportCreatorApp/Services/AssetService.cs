using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using ipx.revit.reports.Models;

namespace ipx.revit.reports.Services
{
    public class AssetService
    {
        private readonly LoggingService _logger;

        public AssetService(string environment = "development")
        {
            _logger = new LoggingService(environment);
        }

        public List<AssetData> ParseAssetJson(string jsonPath)
        {
            try
            {
                if (!File.Exists(jsonPath))
                {
                    _logger.LogWarning($"Asset JSON file not found at: {jsonPath}");
                    return new List<AssetData>();
                }

                string jsonContent = File.ReadAllText(jsonPath);
                _logger.LogDebug($"Raw asset JSON content: {jsonContent}");

                List<AssetData> assets = JsonConvert.DeserializeObject<List<AssetData>>(jsonContent);
                _logger.Log($"Successfully parsed {assets.Count} assets");
                return assets;
            }
            catch (Exception ex)
            {
                _logger.LogError("Exception parsing asset JSON", ex);
                return new List<AssetData>();
            }
        }
    }
} 