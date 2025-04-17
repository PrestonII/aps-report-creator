using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.DB;
using ipx.revit.reports.Utilities;

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
        /// <returns>The curve loop representing the boundary</returns>
        public static CurveLoop CreateViewBoundaryForLevel(Document doc, View view, string levelName)
        {
            try
            {
                LoggingService.Log($"Creating view boundary for level {levelName}");
                LoggingService.Log($"View details: Name={view.Name}, ID={view.Id.IntegerValue}, Type={view.GetType().Name}");

                // Get the level - can't use the view.LevelId (not sure why)
                FilteredElementCollector collector = new FilteredElementCollector(doc);
                IList<Element> levels = collector
                    .OfClass(typeof(Level))
                    .WhereElementIsNotElementType()
                    .ToElements();

                Level level = levels.FirstOrDefault(e => e.Name == levelName) as Level;
                if (level == null)
                {
                    var msg = $"Level {levelName} not found";
                    LoggingService.LogError(msg);
                    throw new Exception(msg);
                }

                LoggingService.Log($"Found level: {level.Name} (ID: {level.Id.IntegerValue}, Elevation: {level.Elevation})");

                // Get the bounding box of the level
                BoundingBoxXYZ boundingBox = GetLevelBoundingBox(doc, level);
                if (boundingBox == null)
                {
                    var msg = $"Could not get bounding box for level {levelName}";
                    LoggingService.LogError(msg);
                    throw new Exception(msg);
                }

                LoggingService.Log($"Got bounding box for level {levelName}:");
                LoggingService.Log($"Min: ({boundingBox.Min.X}, {boundingBox.Min.Y}, {boundingBox.Min.Z})");
                LoggingService.Log($"Max: ({boundingBox.Max.X}, {boundingBox.Max.Y}, {boundingBox.Max.Z})");

                // Create a curve loop from the bounding box
                CurveLoop curveLoop = CreateBoundaryCurveLoop(boundingBox);

                // Apply the crop region to the view
                using (Transaction tx = new Transaction(doc, "Apply Crop Region"))
                {
                    tx.Start();
                    try
                    {
                        // Enable crop box if not already enabled
                        if (!view.CropBoxActive)
                        {
                            view.CropBoxActive = true;
                            LoggingService.Log("Enabled crop box for view");
                        }

                        // Get the crop region manager and set the crop shape
                        ViewCropRegionShapeManager manager = view.GetCropRegionShapeManager();
                        manager.SetCropShape(curveLoop);
                        LoggingService.Log($"Successfully applied crop region to view {view.Name}");

                        tx.Commit();
                        LoggingService.Log("Successfully committed transaction");
                    }
                    catch (Exception ex)
                    {
                        tx.RollBack();
                        LoggingService.LogError($"Error applying crop region: {ex.Message}");
                        LoggingService.LogError($"Stack trace: {ex.StackTrace}");
                        throw;
                    }
                }

                return curveLoop; // Return the curve loop for reuse
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error in CreateViewBoundaryForLevel: {ex.Message}");
                LoggingService.LogError($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Applies a crop region to a view using a curve loop
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="view">The view to apply the crop region to</param>
        /// <param name="curveLoop">The curve loop to use for the crop region</param>
        public static void ApplyCropRegionToView(Document doc, View view, CurveLoop curveLoop)
        {
            try
            {
                LoggingService.Log($"Applying crop region to view {view.Name}");

                using (Transaction tx = new Transaction(doc, "Apply Crop Region"))
                {
                    tx.Start();
                    try
                    {
                        // Enable crop box if not already enabled
                        if (!view.CropBoxActive)
                        {
                            view.CropBoxActive = true;
                            LoggingService.Log("Enabled crop box for view");
                        }

                        // Get the crop region manager and set the crop shape
                        ViewCropRegionShapeManager manager = view.GetCropRegionShapeManager();
                        manager.SetCropShape(curveLoop);
                        LoggingService.Log($"Successfully applied crop region to view {view.Name}");

                        tx.Commit();
                        LoggingService.Log("Successfully committed transaction");
                    }
                    catch (Exception ex)
                    {
                        tx.RollBack();
                        LoggingService.LogError($"Error applying crop region: {ex.Message}");
                        LoggingService.LogError($"Stack trace: {ex.StackTrace}");
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error in ApplyCropRegionToView: {ex.Message}");
                LoggingService.LogError($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Applies a crop region to a view using a bounding box
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="view">The view to apply the crop region to</param>
        /// <param name="boundingBox">The bounding box to use for the crop region</param>
        /// <returns>The curve loop that was created and applied</returns>
        public static CurveLoop ApplyCropRegionToView(Document doc, View view, BoundingBoxXYZ boundingBox)
        {
            try
            {
                LoggingService.Log($"Applying crop region to view {view.Name}");

                // Create a curve loop from the bounding box
                CurveLoop curveLoop = CreateBoundaryCurveLoop(boundingBox);

                using (Transaction tx = new Transaction(doc, "Apply Crop Region"))
                {
                    tx.Start();
                    try
                    {
                        // Enable crop box if not already enabled
                        if (!view.CropBoxActive)
                        {
                            view.CropBoxActive = true;
                            LoggingService.Log("Enabled crop box for view");
                        }

                        // Get the crop region manager and set the crop shape
                        ViewCropRegionShapeManager manager = view.GetCropRegionShapeManager();
                        manager.SetCropShape(curveLoop);
                        LoggingService.Log($"Successfully applied crop region to view {view.Name}");

                        tx.Commit();
                        LoggingService.Log("Successfully committed transaction");
                    }
                    catch (Exception ex)
                    {
                        tx.RollBack();
                        LoggingService.LogError($"Error applying crop region: {ex.Message}");
                        LoggingService.LogError($"Stack trace: {ex.StackTrace}");
                        throw;
                    }
                }

                return curveLoop; // Return the curve loop for reuse
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error in ApplyCropRegionToView: {ex.Message}");
                LoggingService.LogError($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Creates a curve loop representing the boundary
        /// </summary>
        /// <param name="boundingBox">The bounding box to create the curve loop from</param>
        /// <returns>A curve loop representing the boundary</returns>
        private static CurveLoop CreateBoundaryCurveLoop(BoundingBoxXYZ boundingBox)
        {
            try
            {
                LoggingService.Log("Creating boundary curve loop from bounding box");
                LoggingService.Log($"Bounding box: Min({boundingBox.Min.X}, {boundingBox.Min.Y}, {boundingBox.Min.Z}), Max({boundingBox.Max.X}, {boundingBox.Max.Y}, {boundingBox.Max.Z})");

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

                LoggingService.Log("Successfully created boundary curve loop");
                return curveLoop;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error creating boundary curve loop: {ex.Message}");
                LoggingService.LogError($"Stack trace: {ex.StackTrace}");
                throw;
            }
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
                LoggingService.Log($"Getting bounding box for level {level.Name} (ID: {level.Id.IntegerValue}, Elevation: {level.Elevation})");

                // Create a filter to find all model elements at the specified level
                LogicalAndFilter andFilter = ElementFilterUtility.CreateModelElementsAtLevelFilter(level.Id);

                // Apply the filter to find elements in the main document
                FilteredElementCollector collector = new FilteredElementCollector(doc);
                IList<Element> elements = collector.WherePasses(andFilter).ToElements();

                LoggingService.Log($"Found {elements.Count} elements in main document at level {level.Name}");

                // Get elements from linked files
                IList<RevitLinkInstance> linkInstances = new FilteredElementCollector(doc)
                    .OfClass(typeof(RevitLinkInstance))
                    .Cast<RevitLinkInstance>()
                    .ToList();

                LoggingService.Log($"Found {linkInstances.Count} linked files to check for elements");

                foreach (RevitLinkInstance linkInstance in linkInstances)
                {
                    Document linkDoc = linkInstance.GetLinkDocument();
                    if (linkDoc != null)
                    {
                        LoggingService.Log($"Processing linked document: {linkDoc.Title}");

                        // Find the corresponding level in the linked document using RevitLevelService
                        Level linkLevel = RevitLevelService.FindCorrespondingLevel(linkDoc, level);
                        if (linkLevel != null)
                        {
                            LoggingService.Log($"Found corresponding level {linkLevel.Name} in linked document");

                            // Create the same filters for the linked document
                            LogicalAndFilter linkAndFilter = ElementFilterUtility.CreateModelElementsAtLevelFilter(linkLevel.Id);

                            // Get elements from the linked document
                            FilteredElementCollector linkCollector = new FilteredElementCollector(linkDoc);
                            IList<Element> linkElements = linkCollector.WherePasses(linkAndFilter).ToElements();

                            LoggingService.Log($"Found {linkElements.Count} elements in linked document at level {linkLevel.Name}");

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

                                    LoggingService.Log($"Added transformed element {linkElement.Id.IntegerValue} from linked document");
                                }
                            }
                        }
                        else
                        {
                            LoggingService.LogWarning($"Could not find corresponding level in linked document {linkDoc.Title}");
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
                        LoggingService.Log($"Processing element {element.Id.IntegerValue} of type {element.GetType().Name}");
                        LoggingService.Log($"Element bounds: Min({elementBox.Min.X}, {elementBox.Min.Y}, {elementBox.Min.Z}), Max({elementBox.Max.X}, {elementBox.Max.Y}, {elementBox.Max.Z})");

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

                LoggingService.Log($"Final bounding box for level {level.Name}:");
                LoggingService.Log($"Min: ({boundingBox.Min.X}, {boundingBox.Min.Y}, {boundingBox.Min.Z})");
                LoggingService.Log($"Max: ({boundingBox.Max.X}, {boundingBox.Max.Y}, {boundingBox.Max.Z})");

                return boundingBox;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error getting level bounding box: {ex.Message}");
                LoggingService.LogError($"Stack trace: {ex.StackTrace}");
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
            try
            {
                LoggingService.Log($"Finding corresponding level for {mainLevel.Name} in linked document {linkDoc.Title}");
                
                // Use the method from RevitLevelService
                Level correspondingLevel = RevitLevelService.FindCorrespondingLevel(linkDoc, mainLevel);
                
                if (correspondingLevel != null)
                {
                    LoggingService.Log($"Found corresponding level: {correspondingLevel.Name} (ID: {correspondingLevel.Id.IntegerValue})");
                }
                else
                {
                    LoggingService.LogWarning($"No corresponding level found for {mainLevel.Name} in linked document {linkDoc.Title}");
                }
                
                return correspondingLevel;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error finding corresponding level: {ex.Message}");
                LoggingService.LogError($"Stack trace: {ex.StackTrace}");
                return null;
            }
        }
    }
}