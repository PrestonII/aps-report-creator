using System;
using System.Collections.Generic;
using System.Linq;

using ipx.revit.reports.Models;

namespace ipx.revit.reports.Services
{
    /// <summary>
    /// Service for handling view-related operations that can be tested independently of Revit
    /// </summary>
    public static class ViewService
    {
        /// <summary>
        /// Groups views by level name
        /// </summary>
        /// <param name="views">The list of views to group</param>
        /// <returns>A dictionary of level names to lists of views</returns>
        public static Dictionary<string, List<IPXView>> GroupViewsByLevel(List<IPXView> views)
        {
            Dictionary<string, List<IPXView>> viewsByLevel = new Dictionary<string, List<IPXView>>();
            
            foreach (IPXView view in views)
            {
                string levelName = view.LevelName;
                
                if (!viewsByLevel.ContainsKey(levelName))
                {
                    viewsByLevel[levelName] = new List<IPXView>();
                }
                
                viewsByLevel[levelName].Add(view);
            }
            
            return viewsByLevel;
        }

        /// <summary>
        /// Finds the best fitting view within the specified constraints
        /// </summary>
        /// <param name="views">The list of views to check</param>
        /// <param name="maxWidth">The maximum width in feet</param>
        /// <param name="maxHeight">The maximum height in feet</param>
        /// <returns>The best fitting view, or null if none found</returns>
        public static IPXView FindBestFittingView(List<IPXView> views, double maxWidth, double maxHeight)
        {
            if (views == null || !views.Any())
                return null;

            // Sort views by scale (larger scale = more detail)
            var sortedViews = views.OrderByDescending(v => v.Scale).ToList();
            
            foreach (IPXView view in sortedViews)
            {
                // Check if the view fits within the constraints
                if (view.Width <= maxWidth && view.Height <= maxHeight)
                {
                    return view;
                }
            }
            
            return null;
        }

        /// <summary>
        /// Extracts the level name from a view name
        /// </summary>
        /// <param name="viewName">The view name</param>
        /// <returns>The level name</returns>
        public static string ExtractLevelNameFromViewName(string viewName)
        {
            if (string.IsNullOrEmpty(viewName))
                return string.Empty;

            // Extract the level name from the view name (format: "Level Name - Scale X")
            string[] parts = viewName.Split(new[] { " - Scale " }, StringSplitOptions.None);
            
            if (parts.Length > 0)
            {
                return parts[0];
            }
            
            return viewName;
        }

        /// <summary>
        /// Extracts the scale from a view name
        /// </summary>
        /// <param name="viewName">The view name</param>
        /// <returns>The scale value</returns>
        public static int ExtractScaleFromViewName(string viewName)
        {
            if (string.IsNullOrEmpty(viewName))
                return 0;

            // Extract the scale from the view name (format: "Level Name - Scale X")
            string[] parts = viewName.Split(new[] { " - Scale " }, StringSplitOptions.None);
            
            if (parts.Length > 1)
            {
                string scaleString = parts[1];
                
                // Try to parse the scale value
                if (int.TryParse(scaleString, out int scale))
                {
                    return scale;
                }
            }
            
            return 0;
        }
    }
} 