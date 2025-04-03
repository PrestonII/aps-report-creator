using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using ipx.revit.reports.Models;

namespace ipx.revit.reports.Services
{
    public class AssetService
    {
        public List<AssetData> ParseAssetJson(string jsonPath)
        {
            try
            {
                if (!File.Exists(jsonPath))
                {
                    Console.WriteLine($"[WARNING] Asset JSON file not found at: {jsonPath}");
                    return new List<AssetData>();
                }

                string jsonContent = File.ReadAllText(jsonPath);
                Console.WriteLine($"[DEBUG] Raw asset JSON content: {jsonContent}");

                List<AssetData> assets = JsonConvert.DeserializeObject<List<AssetData>>(jsonContent);
                Console.WriteLine($"[INFO] Successfully parsed {assets.Count} assets");
                return assets;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Exception parsing asset JSON: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
                return new List<AssetData>();
            }
        }
    }
} 