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

                // Determine the upper constraint for this level
                double upperConstraint = GetLevelUpperConstraint(doc, level);
                LoggingService.Log($"Level {level.Name} has upper constraint at elevation {upperConstraint}");

                // Get all elements that intersect with the level's height range (including linked files)
                List<Element> elements = ElementFilterUtility.GetElementsIntersectingLevelHeight(doc, level, upperConstraint);
                LoggingService.Log($"Found {elements.Count} elements that intersect with level {level.Name}");

                if (elements.Count == 0)
                {
                    LoggingService.LogWarning($"No model elements found at level {level.Name}");
                    return null;
                }

                // Create a bounding box that encompasses all elements
                BoundingBoxXYZ boundingBox = new BoundingBoxXYZ();
                boundingBox.Min = new XYZ(double.MaxValue, double.MaxValue, level.Elevation);
                boundingBox.Max = new XYZ(double.MinValue, double.MinValue, upperConstraint);

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
                            level.Elevation // Keep the Z at the level elevation
                        );

                        boundingBox.Max = new XYZ(
                            Math.Max(boundingBox.Max.X, elementBox.Max.X),
                            Math.Max(boundingBox.Max.Y, elementBox.Max.Y),
                            upperConstraint // Keep the Z at the upper constraint
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
        /// Gets the upper constraint (elevation) for a level
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="level">The level to get the upper constraint for</param>
        /// <returns>The upper constraint elevation</returns>
        private static double GetLevelUpperConstraint(Document doc, Level level)
        {
            try
            {
                // Get all levels in the document
                FilteredElementCollector collector = new FilteredElementCollector(doc);
                IList<Element> levels = collector.OfClass(typeof(Level)).ToElements();

                // Sort levels by elevation
                List<Level> sortedLevels = levels
                    .Cast<Level>()
                    .OrderBy(l => l.Elevation)
                    .ToList();

                // Find the index of the current level
                int currentIndex = sortedLevels.FindIndex(l => l.Id == level.Id);

                if (currentIndex >= 0 && currentIndex < sortedLevels.Count - 1)
                {
                    // If there's a level above, use its elevation as the upper constraint
                    Level nextLevel = sortedLevels[currentIndex + 1];
                    LoggingService.Log($"Level {level.Name} has next level {nextLevel.Name} at elevation {nextLevel.Elevation}");
                    return nextLevel.Elevation;
                }
                else
                {
                    // If this is the highest level, add 1 foot to its elevation
                    double upperConstraint = level.Elevation + 1.0;
                    LoggingService.Log($"Level {level.Name} is the highest level, using elevation + 1 foot: {upperConstraint}");
                    return upperConstraint;
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error getting level upper constraint: {ex.Message}");
                LoggingService.LogError($"Stack trace: {ex.StackTrace}");
                // Default to 1 foot above the level if there's an error
                return level.Elevation + 1.0;
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