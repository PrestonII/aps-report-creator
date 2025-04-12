using System.Collections.Generic;
using Newtonsoft.Json;

namespace ipx.revit.reports.Models
{
    /// <summary>
    /// Class to represent the project data from the input JSON
    /// </summary>
    public class ProjectData
    {
        [JsonProperty("projectName")]
        public string ProjectName { get; set; } = string.Empty;
        
        [JsonProperty("projectNumber")]
        public string ProjectNumber { get; set; } = string.Empty;
        
        [JsonProperty("reportType")]
        public string ReportType { get; set; } = string.Empty;
        
        [JsonProperty("viewTypes")]
        public List<string> ViewTypes { get; set; } = new List<string>();
        
        [JsonProperty("viewFilters")]
        public List<ViewFilter> ViewFilters { get; set; } = new List<ViewFilter>();
        
        [JsonProperty("maxViews")]
        public int MaxViews { get; set; } = 0;
        
        [JsonProperty("outputFileName")]
        public string OutputFileName { get; set; } = string.Empty;
        
        [JsonProperty("authentication")]
        public AuthenticationInfo? Authentication { get; set; }
        
        [JsonProperty("imageData")]
        public List<AssetData> ImageData { get; set; } = new List<AssetData>();

        [JsonProperty("environment")]
        public string Environment { get; set; } = "debug";
    }
    
    /// <summary>
    /// Authentication information for API access
    /// </summary>
    public class AuthenticationInfo
    {
        [JsonProperty("username")]
        public string Username { get; set; } = string.Empty;
        
        [JsonProperty("password")]
        public string Password { get; set; } = string.Empty;
    }
} 