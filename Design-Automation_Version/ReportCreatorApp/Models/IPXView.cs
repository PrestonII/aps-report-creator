using System;

namespace ipx.revit.reports.Models
{
    /// <summary>
    /// Represents a view in the application
    /// </summary>
    public class IPXView
    {
        /// <summary>
        /// Gets or sets the view ID
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the view name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the view type
        /// </summary>
        public string ViewType { get; set; }

        /// <summary>
        /// Gets or sets the view scale
        /// </summary>
        public int Scale { get; set; }

        /// <summary>
        /// Gets or sets the level name associated with this view
        /// </summary>
        public string LevelName { get; set; }

        /// <summary>
        /// Gets or sets the width of the view in feet
        /// </summary>
        public double Width { get; set; }

        /// <summary>
        /// Gets or sets the height of the view in feet
        /// </summary>
        public double Height { get; set; }

        /// <summary>
        /// Gets or sets the Revit ElementId of the view
        /// </summary>
        public string RevitElementId { get; set; }

        /// <summary>
        /// Gets or sets whether the view can be placed on a sheet
        /// </summary>
        public bool CanBePlacedOnSheet { get; set; }

        /// <summary>
        /// Creates a new instance of the IPXView class
        /// </summary>
        public IPXView()
        {
        }

        /// <summary>
        /// Creates a new instance of the IPXView class with the specified properties
        /// </summary>
        /// <param name="id">The view ID</param>
        /// <param name="name">The view name</param>
        /// <param name="viewType">The view type</param>
        /// <param name="scale">The view scale</param>
        /// <param name="levelName">The level name</param>
        /// <param name="width">The width of the view in feet</param>
        /// <param name="height">The height of the view in feet</param>
        /// <param name="revitElementId">The Revit ElementId of the view</param>
        /// <param name="canBePlacedOnSheet">Whether the view can be placed on a sheet</param>
        public IPXView(string id, string name, string viewType, int scale, string levelName, double width, double height, string revitElementId, bool canBePlacedOnSheet)
        {
            Id = id;
            Name = name;
            ViewType = viewType;
            Scale = scale;
            LevelName = levelName;
            Width = width;
            Height = height;
            RevitElementId = revitElementId;
            CanBePlacedOnSheet = canBePlacedOnSheet;
        }
    }
} 