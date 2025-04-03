using System.Collections.Generic;
using Newtonsoft.Json;

namespace ipx.revit.reports.Models
{
    /// <summary>
    /// Class to represent an asset from the input JSON
    /// </summary>
    public class AssetData
    {
        [JsonProperty("asset_id")]
        public string AssetId { get; set; }
        
        [JsonProperty("project")]
        public string Project { get; set; }
        
        [JsonProperty("asset_type")]
        public string AssetType { get; set; }
        
        [JsonProperty("image_subtype")]
        public string ImageSubtype { get; set; }
        
        [JsonProperty("asset_name")]
        public string AssetName { get; set; }
        
        [JsonProperty("asset_url_override")]
        public string AssetUrlOverride { get; set; }
        
        [JsonProperty("asset_url")]
        public string AssetUrl { get; set; }
    }
} 