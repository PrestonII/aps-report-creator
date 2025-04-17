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
        /// Gets the bounding box of a level
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="level">The level to get the bounding box for</param>
        /// <returns>The bounding box of the level</returns>
        private static BoundingBoxXYZ GetLevelBoundingBox(Document doc, Level level)
        {
            try
            {
                // Get all linked documents
                FilteredElementCollector collectorLinks = new FilteredElementCollector(doc);
                IList<Element> revitLinks = collectorLinks.OfClass(typeof(RevitLinkInstance)).ToElements();

                if (revitLinks.Count == 0)
                {
                    LoggingService.LogWarning("No linked Revit files found");
                    return null;
                }

                // Create a bounding box that encompasses all elements
                BoundingBoxXYZ boundingBox = new BoundingBoxXYZ();
                bool first = true;

                foreach (Element linkElement in revitLinks)
                {
                    RevitLinkInstance linkInstance = linkElement as RevitLinkInstance;
                    Document linkDoc = linkInstance.GetLinkDocument();

                    if (linkDoc != null)
                    {
                        // Get all model elements in the linked document
                        FilteredElementCollector collector = new FilteredElementCollector(linkDoc);
                        IList<Element> elements = collector
                            .OfClass(typeof(Element))
                            .WhereElementIsNotElementType()
                            .ToElements();

                        // Filter elements at the level
                        var levelElements = elements.Where(e => 
                        {
                            // Check if the element has a level parameter
                            Parameter levelParam = e.get_Parameter(BuiltInParameter.INSTANCE_FREE_HOST_PARAM);
                            if (levelParam != null && levelParam.AsElementId() == level.Id)
                                return true;

                            // Check if the element is hosted by the level
                            if (e is FamilyInstance fi && fi.Host != null && fi.Host.Id == level.Id)
                                return true;

                            return false;
                        }).ToList();

                        foreach (Element element in levelElements)
                        {
                            BoundingBoxXYZ elementBox = element.get_BoundingBox(null);
                            if (elementBox != null)
                            {
                                // Transform the bounding box from link space to host space
                                Transform transform = linkInstance.GetTotalTransform();
                                XYZ min = transform.OfPoint(elementBox.Min);
                                XYZ max = transform.OfPoint(elementBox.Max);

                                if (first)
                                {
                                    boundingBox.Min = min;
                                    boundingBox.Max = max;
                                    first = false;
                                }
                                else
                                {
                                    boundingBox.Min = new XYZ(
                                        Math.Min(boundingBox.Min.X, min.X),
                                        Math.Min(boundingBox.Min.Y, min.Y),
                                        Math.Min(boundingBox.Min.Z, min.Z)
                                    );
                                    boundingBox.Max = new XYZ(
                                        Math.Max(boundingBox.Max.X, max.X),
                                        Math.Max(boundingBox.Max.Y, max.Y),
                                        Math.Max(boundingBox.Max.Z, max.Z)
                                    );
                                }
                            }
                        }
                    }
                }

                if (first)
                {
                    LoggingService.LogWarning($"No elements found at level {level.Name} in any linked documents");
                    return null;
                }

                // Add some padding to the bounding box
                XYZ padding = new XYZ(10, 10, 10);
                boundingBox.Min = boundingBox.Min.Subtract(padding);
                boundingBox.Max = boundingBox.Max.Add(padding);

                return boundingBox;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error getting level bounding box: {ex.Message}");
                return null;
            }
        }
    }
} 