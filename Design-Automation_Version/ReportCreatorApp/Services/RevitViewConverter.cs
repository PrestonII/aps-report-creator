using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.DB;

using ipx.revit.reports.Models;

namespace ipx.revit.reports.Services
{
    /// <summary>
    /// Service for converting between Revit views and IPXView models
    /// </summary>
    public static class RevitViewConverter
    {
        /// <summary>
        /// Converts a Revit ViewPlan to an IPXView
        /// </summary>
        /// <param name="view">The Revit ViewPlan</param>
        /// <returns>An IPXView</returns>
        public static IPXView ConvertToIPXView(ViewPlan view)
        {
            if (view == null)
                return null;

            // Extract the level name from the view name
            string levelName = ViewService.ExtractLevelNameFromViewName(view.Name);
            
            // Get the view's dimensions
            double viewWidth = 0;
            double viewHeight = 0;
            
            // For floor plan views, we need to use the crop box
            if (view.CropBoxActive)
            {
                // Get the crop box
                BoundingBoxXYZ cropBox = view.CropBox;
                
                // Calculate the dimensions in feet
                viewWidth = (cropBox.Max.X - cropBox.Min.X);
                viewHeight = (cropBox.Max.Y - cropBox.Min.Y);
            }
            else
            {
                // If no crop box, use the outline
                BoundingBoxUV outline = view.Outline;
                viewWidth = (outline.Max.U - outline.Min.U);
                viewHeight = (outline.Max.V - outline.Min.V);
            }
            
            // Convert to paper space dimensions (divide by scale)
            viewWidth = viewWidth / view.Scale;
            viewHeight = viewHeight / view.Scale;
            
            // Create the IPXView
            return new IPXView(
                view.Id.IntegerValue.ToString(),
                view.Name,
                view.ViewType.ToString(),
                view.Scale,
                levelName,
                viewWidth,
                viewHeight,
                view.Id.IntegerValue.ToString(),
                true // Floor plans can be placed on sheets
            );
        }

        /// <summary>
        /// Converts a list of Revit ViewPlans to IPXViews
        /// </summary>
        /// <param name="views">The list of Revit ViewPlans</param>
        /// <returns>A list of IPXViews</returns>
        public static List<IPXView> ConvertToIPXViews(IEnumerable<ViewPlan> views)
        {
            if (views == null)
                return new List<IPXView>();

            return views.Select(v => ConvertToIPXView(v)).ToList();
        }

        /// <summary>
        /// Converts a Revit View to an IPXView
        /// </summary>
        /// <param name="view">The Revit View</param>
        /// <returns>An IPXView</returns>
        public static IPXView ConvertToIPXView(View view)
        {
            if (view == null)
                return null;

            // Check if the view can be placed on a sheet
            bool canBePlacedOnSheet = view.ViewType.ToString().Contains("Drafting");
            
            // Create the IPXView
            return new IPXView(
                view.Id.IntegerValue.ToString(),
                view.Name,
                view.ViewType.ToString(),
                view.Scale,
                string.Empty, // No level name for non-floor plan views
                0, // Width not applicable for non-floor plan views
                0, // Height not applicable for non-floor plan views
                view.Id.IntegerValue.ToString(),
                canBePlacedOnSheet
            );
        }

        /// <summary>
        /// Gets the ElementId from an IPXView
        /// </summary>
        /// <param name="view">The IPXView</param>
        /// <returns>The ElementId</returns>
        public static ElementId GetElementId(IPXView view)
        {
            if (view == null || string.IsNullOrEmpty(view.RevitElementId))
                return ElementId.InvalidElementId;

            if (int.TryParse(view.RevitElementId, out int id))
            {
                return new ElementId(id);
            }

            return ElementId.InvalidElementId;
        }
    }
} 