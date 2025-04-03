using System.Collections.Generic;
using Autodesk.Revit.DB;
using Newtonsoft.Json;

namespace ipx.revit.reports.Models
{
    /// <summary>
    /// Class to represent the project data from the input JSON
    /// </summary>
    public class ProjectData
    {
        [JsonProperty("projectName")]
        public string ProjectName { get; set; }
        
        [JsonProperty("projectNumber")]
        public string ProjectNumber { get; set; }
        
        [JsonProperty("reportType")]
        public string ReportType { get; set; }
        
        [JsonProperty("viewTypes")]
        public List<ViewType> ViewTypes { get; set; } = new List<ViewType>();
        
        [JsonProperty("viewFilters")]
        public List<ViewFilter> ViewFilters { get; set; } = new List<ViewFilter>();
        
        [JsonProperty("maxViews")]
        public int MaxViews { get; set; } = 0;
        
        [JsonProperty("outputFileName")]
        public string OutputFileName { get; set; }
        
        [JsonProperty("authentication")]
        public AuthenticationInfo Authentication { get; set; }
        
        [JsonProperty("imageData")]
        public List<AssetData> ImageData { get; set; } = new List<AssetData>();
    }
    
    /// <summary>
    /// Authentication information for API access
    /// </summary>
    public class AuthenticationInfo
    {
        [JsonProperty("username")]
        public string Username { get; set; }
        
        [JsonProperty("password")]
        public string Password { get; set; }
    }
} 