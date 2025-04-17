using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;

namespace ipx.revit.reports.Services
{
    /// <summary>
    /// Service for handling view boundaries in Revit
    /// </summary>
    public static class RevitScopeBoxService
    {
        /// <summary>
        /// Creates a view boundary for a level
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="view">The view to create the boundary in</param>
        /// <param name="levelName">The name of the level</param>
        /// <returns>The created view boundary element</returns>
        public static Element CreateViewBoundaryForLevel(Document doc, View view, string levelName)
        {
            try
            {
                LoggingService.Log($"Creating view boundary for level {levelName}");

                // Get the level
                FilteredElementCollector collector = new FilteredElementCollector(doc);
                IList<Element> levels = collector
                    .OfClass(typeof(Level))
                    .WhereElementIsNotElementType()
                    .ToElements();

                Level level = levels.FirstOrDefault(e => e.Name == levelName) as Level;
                if (level == null)
                {
                    LoggingService.LogError($"Level {levelName} not found");
                    return null;
                }

                // Get the bounding box of the level
                BoundingBoxXYZ boundingBox = GetLevelBoundingBox(doc, level);
                if (boundingBox == null)
                {
                    LoggingService.LogError($"Could not get bounding box for level {levelName}");
                    return null;
                }

                // Create the view boundary
                using (Transaction tx = new Transaction(doc, "Create View Boundary"))
                {
                    tx.Start();

                    // Create a detail curve to represent the boundary
                    CurveLoop curveLoop = CreateBoundaryCurveLoop(boundingBox);
                    CurveArray curveArray = new CurveArray();
                    foreach (Curve curve in curveLoop)
                    {
                        curveArray.Append(curve);
                    }
                    
                    // Create the detail curves in the view
                    DetailCurveArray detailCurves = doc.Create.NewDetailCurveArray(view, curveArray);
                    
                    // Get the first detail curve to use as our boundary element
                    Element boundaryElement = null;
                    if (detailCurves.Size > 0)
                    {
                        boundaryElement = doc.GetElement(detailCurves.get_Item(0).Id);
                        if (boundaryElement != null)
                        {
                            boundaryElement.Name = $"View Boundary - {levelName}";
                        }
                    }

                    tx.Commit();
                    return boundaryElement;
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error creating view boundary: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Applies a view boundary to a view
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="view">The view to apply the boundary to</param>
        /// <param name="boundaryElement">The boundary element to apply</param>
        public static void ApplyViewBoundary(Document doc, View view, Element boundaryElement)
        {
            try
            {
                LoggingService.Log($"Applying view boundary to view {view.Name}");

                using (Transaction tx = new Transaction(doc, "Apply View Boundary"))
                {
                    tx.Start();

                    // Get the bounding box of the boundary element
                    BoundingBoxXYZ boundaryBox = boundaryElement.get_BoundingBox(null);
                    if (boundaryBox != null)
                    {
                        // Set the view's crop box to match the boundary
                        view.CropBox = boundaryBox;
                        view.CropBoxVisible = true;
                    }

                    tx.Commit();
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error applying view boundary: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a curve loop representing the boundary
        /// </summary>
        /// <param name="boundingBox">The bounding box to create the curve loop from</param>
        /// <returns>A curve loop representing the boundary</returns>
        private static CurveLoop CreateBoundaryCurveLoop(BoundingBoxXYZ boundingBox)
        {
            CurveLoop curveLoop = new CurveLoop();
            
            // Create the four corners of the boundary
            XYZ min = boundingBox.Min;
            XYZ max = boundingBox.Max;
            
            XYZ p1 = new XYZ(min.X, min.Y, min.Z);
            XYZ p2 = new XYZ(max.X, min.Y, min.Z);
            XYZ p3 = new XYZ(max.X, max.Y, min.Z);
            XYZ p4 = new XYZ(min.X, max.Y, min.Z);

            // Create the four lines of the boundary
            curveLoop.Append(Line.CreateBound(p1, p2));
            curveLoop.Append(Line.CreateBound(p2, p3));
            curveLoop.Append(Line.CreateBound(p3, p4));
            curveLoop.Append(Line.CreateBound(p4, p1));

            return curveLoop;
        }

        /// <summary>
        /// Gets the bounding box for a level by finding the extents of all model elements at that level
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="level">The level to get the bounding box for</param>
        /// <returns>The bounding box for the level</returns>
        public static BoundingBoxXYZ GetLevelBoundingBox(Document doc, Level level)
        {
            try
            {
                LoggingService.Log($"Getting bounding box for level {level.Name}");
                
                // Create a filter to find all model elements at the specified level
                // Only include Walls, Roofs, and Floors as requested
                ElementClassFilter wallFilter = new ElementClassFilter(typeof(Wall));
                ElementClassFilter roofFilter = new ElementClassFilter(typeof(RoofBase));
                ElementClassFilter floorFilter = new ElementClassFilter(typeof(Floor));
                
                LogicalOrFilter orFilter = new LogicalOrFilter(wallFilter, roofFilter);
                orFilter = new LogicalOrFilter(orFilter, floorFilter);
                
                // Create a filter to find elements at the specified level
                ElementLevelFilter levelFilter = new ElementLevelFilter(level.Id);
                
                // Combine the filters
                LogicalAndFilter andFilter = new LogicalAndFilter(orFilter, levelFilter);
                
                // Apply the filter to find elements in the main document
                FilteredElementCollector collector = new FilteredElementCollector(doc);
                IList<Element> elements = collector.WherePasses(andFilter).ToElements();
                
                // Get elements from linked files
                IList<RevitLinkInstance> linkInstances = new FilteredElementCollector(doc)
                    .OfClass(typeof(RevitLinkInstance))
                    .Cast<RevitLinkInstance>()
                    .ToList();
                
                foreach (RevitLinkInstance linkInstance in linkInstances)
                {
                    Document linkDoc = linkInstance.GetLinkDocument();
                    if (linkDoc != null)
                    {
                        // Find the corresponding level in the linked document
                        Level linkLevel = FindCorrespondingLevel(linkDoc, level);
                        if (linkLevel != null)
                        {
                            // Create the same filters for the linked document
                            ElementClassFilter linkWallFilter = new ElementClassFilter(typeof(Wall));
                            ElementClassFilter linkRoofFilter = new ElementClassFilter(typeof(RoofBase));
                            ElementClassFilter linkFloorFilter = new ElementClassFilter(typeof(Floor));
                            
                            LogicalOrFilter linkOrFilter = new LogicalOrFilter(linkWallFilter, linkRoofFilter);
                            linkOrFilter = new LogicalOrFilter(linkOrFilter, linkFloorFilter);
                            
                            ElementLevelFilter linkLevelFilter = new ElementLevelFilter(linkLevel.Id);
                            LogicalAndFilter linkAndFilter = new LogicalAndFilter(linkOrFilter, linkLevelFilter);
                            
                            // Get elements from the linked document
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
                                    
                                    // Create a new bounding box in the main document's coordinate system
                                    BoundingBoxXYZ transformedBox = new BoundingBoxXYZ();
                                    transformedBox.Min = min;
                                    transformedBox.Max = max;
                                    
                                    // Add the transformed box to our collection
                                    elements.Add(linkElement);
                                }
                            }
                        }
                    }
                }
                
                if (elements.Count == 0)
                {
                    LoggingService.LogWarning($"No model elements found at level {level.Name}");
                    return null;
                }
                
                // Create a bounding box that encompasses all elements
                BoundingBoxXYZ boundingBox = new BoundingBoxXYZ();
                boundingBox.Min = new XYZ(double.MaxValue, double.MaxValue, double.MaxValue);
                boundingBox.Max = new XYZ(double.MinValue, double.MinValue, double.MinValue);
                
                foreach (Element element in elements)
                {
                    BoundingBoxXYZ elementBox = element.get_BoundingBox(null);
                    if (elementBox != null)
                    {
                        boundingBox.Min = new XYZ(
                            Math.Min(boundingBox.Min.X, elementBox.Min.X),
                            Math.Min(boundingBox.Min.Y, elementBox.Min.Y),
                            Math.Min(boundingBox.Min.Z, elementBox.Min.Z)
                        );
                        
                        boundingBox.Max = new XYZ(
                            Math.Max(boundingBox.Max.X, elementBox.Max.X),
                            Math.Max(boundingBox.Max.Y, elementBox.Max.Y),
                            Math.Max(boundingBox.Max.Z, elementBox.Max.Z)
                        );
                    }
                }
                
                // Add a buffer to the bounding box
                double buffer = 10.0; // 10 feet buffer
                boundingBox.Min = new XYZ(
                    boundingBox.Min.X - buffer,
                    boundingBox.Min.Y - buffer,
                    boundingBox.Min.Z
                );
                
                boundingBox.Max = new XYZ(
                    boundingBox.Max.X + buffer,
                    boundingBox.Max.Y + buffer,
                    boundingBox.Max.Z
                );
                
                return boundingBox;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error getting level bounding box: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Finds the corresponding level in a linked document
        /// </summary>
        /// <param name="linkDoc">The linked document</param>
        /// <param name="mainLevel">The level in the main document</param>
        /// <returns>The corresponding level in the linked document, or null if not found</returns>
        private static Level FindCorrespondingLevel(Document linkDoc, Level mainLevel)
        {
            // First try to find a level with the same name
            FilteredElementCollector collector = new FilteredElementCollector(linkDoc);
            IList<Element> levels = collector.OfClass(typeof(Level)).ToElements();
            
            Level matchingLevel = levels
                .Cast<Level>()
                .FirstOrDefault(l => l.Name == mainLevel.Name);
            
            if (matchingLevel != null)
                return matchingLevel;
            
            // If no exact match, try to find a level at the same elevation
            return levels
                .Cast<Level>()
                .FirstOrDefault(l => Math.Abs(l.Elevation - mainLevel.Elevation) < 0.001);
        }
    }
} 