using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.DB;

using ipx.revit.reports.Services;

namespace ipx.revit.reports.Utilities
{
    /// <summary>
    /// Utility class for creating element filters
    /// </summary>
    public static class ElementFilterUtility
    {
        /// <summary>
        /// Creates a filter for model elements (walls, roofs, floors)
        /// </summary>
        /// <returns>A logical OR filter for model elements</returns>
        public static LogicalOrFilter CreateModelElementFilter()
        {
            ElementClassFilter wallFilter = new ElementClassFilter(typeof(Wall));
            ElementClassFilter roofFilter = new ElementClassFilter(typeof(RoofBase));
            ElementClassFilter floorFilter = new ElementClassFilter(typeof(Floor));

            LogicalOrFilter orFilter = new LogicalOrFilter(wallFilter, roofFilter);
            return new LogicalOrFilter(orFilter, floorFilter);
        }

        /// <summary>
        /// Creates a filter for elements at a specific level
        /// </summary>
        /// <param name="levelId">The ID of the level</param>
        /// <returns>A filter for elements at the specified level</returns>
        public static ElementLevelFilter CreateLevelFilter(ElementId levelId)
        {
            return new ElementLevelFilter(levelId);
        }

        /// <summary>
        /// Creates a combined filter for model elements at a specific level
        /// </summary>
        /// <param name="levelId">The ID of the level</param>
        /// <returns>A combined filter for model elements at the specified level</returns>
        public static LogicalAndFilter CreateModelElementsAtLevelFilter(ElementId levelId)
        {
            LogicalOrFilter modelFilter = CreateModelElementFilter();
            ElementLevelFilter levelFilter = CreateLevelFilter(levelId);
            return new LogicalAndFilter(modelFilter, levelFilter);
        }

        /// <summary>
        /// Gets all model elements that intersect with a level's height range, including elements from linked files
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="level">The level to check</param>
        /// <param name="upperConstraint">The upper constraint elevation for the level</param>
        /// <returns>A list of elements that intersect with the level's height range</returns>
        public static List<Element> GetElementsIntersectingLevelHeight(Document doc, Level level, double upperConstraint)
        {
            try
            {
                List<Element> elements = new List<Element>();

                // 1. Get elements from the main document that have this level as their base level
                LogicalAndFilter andFilter = CreateModelElementsAtLevelFilter(level.Id);
                FilteredElementCollector collector = new FilteredElementCollector(doc);
                List<Element> baseElements = collector.WherePasses(andFilter).ToList();
                elements.AddRange(baseElements);

                // 2. Get all model elements in the main document (Walls, Floors, Roofs)
                LogicalOrFilter modelFilter = CreateModelElementFilter();
                FilteredElementCollector allElementsCollector = new FilteredElementCollector(doc);
                List<Element> allElements = allElementsCollector
                    .WherePasses(modelFilter)
                    .ToList();

                // 3. Find elements in the main document that intersect with our level's height range but have a different base level
                foreach (Element element in allElements)
                {
                    // Skip elements we've already processed
                    if (elements.Contains(element))
                        continue;

                    BoundingBoxXYZ elementBox = element.get_BoundingBox(null);
                    if (elementBox != null)
                    {
                        // Check if the element intersects with our level's height range
                        if (elementBox.Max.Z >= level.Elevation && elementBox.Min.Z <= upperConstraint)
                        {
                            elements.Add(element);
                        }
                    }
                }

                // 4. Get all linked files
                IList<RevitLinkInstance> linkInstances = new FilteredElementCollector(doc)
                    .OfClass(typeof(RevitLinkInstance))
                    .Cast<RevitLinkInstance>()
                    .ToList();

                // 5. Process each linked file
                foreach (RevitLinkInstance linkInstance in linkInstances)
                {
                    Document linkDoc = linkInstance.GetLinkDocument();
                    if (linkDoc != null)
                    {
                        // Find the corresponding level in the linked document
                        Level linkLevel = RevitLevelService.FindCorrespondingLevel(linkDoc, level);
                        if (linkLevel != null)
                        {
                            // Get elements from the linked document that have this level as their base
                            LogicalAndFilter linkAndFilter = CreateModelElementsAtLevelFilter(linkLevel.Id);
                            FilteredElementCollector linkCollector = new FilteredElementCollector(linkDoc);
                            IList<Element> linkElements = linkCollector.WherePasses(linkAndFilter).ToElements();

                            // Transform the elements to the main document's coordinate system
                            Transform transform = linkInstance.GetTotalTransform();
                            foreach (Element linkElement in linkElements)
                            {
                                BoundingBoxXYZ linkBox = linkElement.get_BoundingBox(null);
                                if (linkBox != null)
                                {
                                    // Transform the bounding box corners
                                    XYZ min = transform.OfPoint(linkBox.Min);
                                    XYZ max = transform.OfPoint(linkBox.Max);

                                    // Check if the element intersects with our level's height range
                                    if (max.Z >= level.Elevation && min.Z <= upperConstraint)
                                    {
                                        elements.Add(linkElement);
                                    }
                                }
                            }

                            // Get all model elements in the linked document (Walls, Floors, Roofs)
                            FilteredElementCollector linkAllElementsCollector = new FilteredElementCollector(linkDoc);
                            IList<Element> linkAllElements = linkAllElementsCollector
                                .WherePasses(modelFilter)
                                .ToList();

                            // Find elements in the linked document that intersect with our level's height range but have a different base level
                            foreach (Element linkElement in linkAllElements)
                            {
                                // Skip elements we've already processed
                                if (elements.Contains(linkElement))
                                    continue;

                                BoundingBoxXYZ linkBox = linkElement.get_BoundingBox(null);
                                if (linkBox != null)
                                {
                                    // Transform the bounding box corners
                                    XYZ min = transform.OfPoint(linkBox.Min);
                                    XYZ max = transform.OfPoint(linkBox.Max);

                                    // Check if the element intersects with our level's height range
                                    if (max.Z >= level.Elevation && min.Z <= upperConstraint)
                                    {
                                        elements.Add(linkElement);
                                    }
                                }
                            }
                        }
                    }
                }

                return elements;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error getting elements intersecting level height: {ex.Message}");
                LoggingService.LogError($"Stack trace: {ex.StackTrace}");
                return new List<Element>();
            }
        }
    }
}