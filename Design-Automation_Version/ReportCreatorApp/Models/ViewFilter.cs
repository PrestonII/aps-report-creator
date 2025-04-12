using System.Collections.Generic;

using Newtonsoft.Json;

namespace ipx.revit.reports.Models
{
    /// <summary>
    /// Class to represent a view filter
    /// </summary>
    public class ViewFilter
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("parameters")]
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
    }
}