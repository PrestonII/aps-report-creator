using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.DB;

namespace ipx.revit.reports.Services
{
    /// <summary>
    /// Service for handling view operations in Revit
    /// </summary>
    public static class RevitViewService
    {
        // Dictionary of scale factors for floor plan views
        public static readonly Dictionary<string, int> _scaleFactors = new Dictionary<string, int>
        {
            { "1/4\" = 1'-0\"", 48 },
            { "3/16\" = 1'-0\"", 64 },
            { "1/8\" = 1'-0\"", 96 },
            { "3/32\" = 1'-0\"", 128 },
            { "1/16\" = 1'-0\"", 192 },
            { "1:10", 10 },
            { "1:20", 20 },
            { "1:100", 100 },
            { "1:125", 125 },
            { "1:150", 150 },
            { "1:200", 200 },
            { "1:250", 250 }
        };

        /// <summary>
        /// Creates levels and views based on the linked files
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <returns>A tuple containing the created views and sheets</returns>
        public static (IList<ViewPlan> views, IList<ViewSheet> sheets) CreateLevelsAndViews(Document doc)
        {
            try
            {
                LoggingService.Log("Starting to create levels and views...");
                
                // Get all linked documents
                FilteredElementCollector collectorLinks = new FilteredElementCollector(doc);
                IList<Element> revitLinks = collectorLinks
                    .OfClass(typeof(RevitLinkInstance))
                    .WhereElementIsNotElementType()
                    .ToElements();
                
                if (revitLinks.Count == 0)
                {
                    LoggingService.LogError("No linked Revit files found");
                    return (new List<ViewPlan>(), new List<ViewSheet>());
                }

                LoggingService.Log($"Found {revitLinks.Count} linked Revit files");

                // STEP 1: Collect levels from linked files
                List<Level> levels = RevitLevelService.CollectLevelsFromLinkedFiles(doc, revitLinks);
                if (levels.Count == 0)
                {
                    LoggingService.LogError("No levels found in linked files");
                    return (new List<ViewPlan>(), new List<ViewSheet>());
                }

                // STEP 2: Create levels in the current document
                List<Level> createdLevels = RevitLevelService.CreateLevels(doc, levels);
                LoggingService.Log($"Created {createdLevels.Count} levels");

                // STEP 3: Create floor plan views for each level at each scale
                Dictionary<Level, List<ViewPlan>> viewsByLevel = CreateViewsForLevels(doc, createdLevels);
                int viewCount = viewsByLevel.Values.Sum(views => views.Count);
                LoggingService.Log($"Created {viewCount} views");

                // STEP 4: Create scope boxes for each level using the smallest scale view
                Dictionary<Level, CurveLoop> curveLoopsByLevel = CreateScopeBoxesForLevels(doc, viewsByLevel);

                // STEP 5: Apply scope boxes to all views for each level
                ApplyScopeBoxesToViews(doc, viewsByLevel, curveLoopsByLevel);

                // STEP 6: Create sheets for each level
                List<ViewSheet> createdSheets = CreateSheetsForLevels(doc, createdLevels);
                LoggingService.Log($"Created {createdSheets.Count} sheets");

                // Collect all views into a single list
                List<ViewPlan> allViews = viewsByLevel.Values.SelectMany(v => v).ToList();

                return (allViews, createdSheets);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error creating levels and views: {ex.Message}");
                return (new List<ViewPlan>(), new List<ViewSheet>());
            }
        }

        /// <summary>
        /// Creates sheets for each level
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="levels">The levels to create sheets for</param>
        /// <returns>The created sheets</returns>
        private static List<ViewSheet> CreateSheetsForLevels(Document doc, List<Level> levels)
        {
            List<ViewSheet> createdSheets = new List<ViewSheet>();
            
            foreach (Level level in levels)
            {
                if (level == null || !level.IsValidObject)
                {
                    LoggingService.LogWarning("Invalid level encountered, skipping");
                    continue;
                }

                using (Transaction tx = new Transaction(doc, "Create Sheet"))
                {
                    tx.Start();
                    try
                    {
                        ElementId titleblockId = RevitTitleBlockService.GetTitleblockId(doc, "_SCHEMATIC PLAN TITLEBLOCK");
                        if (titleblockId == ElementId.InvalidElementId)
                        {
                            LoggingService.LogWarning($"Could not find titleblock for level {level.Name}");
                            continue;
                        }

                        ViewSheet sheet = ViewSheet.Create(doc, titleblockId);
                        if (sheet != null)
                        {
                            sheet.Name = $"Level {level.Name}";
                            createdSheets.Add(sheet);
                        }
                        tx.Commit();
                    }
                    catch (Exception ex)
                    {
                        tx.RollBack();
                        LoggingService.LogError($"Error creating sheet for level {level.Name}: {ex.Message}");
                    }
                }
            }
            
            return createdSheets;
        }

        /// <summary>
        /// Creates a floor plan view for a level at a specific scale
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="level">The level to create the view for</param>
        /// <param name="scale">The scale of the view</param>
        /// <returns>The created view</returns>
        private static ViewPlan CreateFloorPlanView(Document doc, Level level, int scale)
        {
            try
            {
                if (level == null || !level.IsValidObject)
                {
                    LoggingService.LogError("Invalid level provided to CreateFloorPlanView");
                    return null;
                }

                LoggingService.Log($"Creating floor plan view for level '{level.Name}' at scale {scale}");

                // Get the floor plan view family type
                ViewFamilyType viewType = GetFloorPlanViewFamilyType(doc);
                if (viewType == null)
                {
                    LoggingService.LogError("Could not get floor plan view family type");
                    return null;
                }

                LoggingService.Log($"Using view type: {viewType.Name}");

                // Create the floor plan view within a transaction
                ViewPlan view = null;
                using (Transaction tx = new Transaction(doc, "Create Floor Plan View"))
                {
                    tx.Start();
                    try
                    {
                        // Create the floor plan view
                        view = ViewPlan.Create(doc, viewType.Id, level.Id);
                        if (view == null)
                        {
                            LoggingService.LogError("Failed to create floor plan view");
                            tx.RollBack();
                            return null;
                        }

                        LoggingService.Log($"Created floor plan view with ID: {view.Id.IntegerValue}");

                        // Set the scale
                        if (scale > 0)
                        {
                            view.Scale = scale;
                            LoggingService.Log($"Set view scale to {scale}");
                        }

                        // Set the view name directly without checking for duplicates
                        string viewName = $"{level.Name} - Scale {scale}";
                        LoggingService.Log($"Setting view name to: {viewName}");
                        view.Name = viewName;
                        
                        tx.Commit();
                        LoggingService.Log($"Successfully created view: {view.Name}");
                    }
                    catch (Exception ex)
                    {
                        tx.RollBack();
                        LoggingService.LogError($"Error creating floor plan view: {ex.Message}");
                        return null;
                    }
                }

                return view;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error in CreateFloorPlanView: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Creates views for each level
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="levels">The levels to create views for</param>
        /// <returns>A dictionary of levels and their views</returns>
        private static Dictionary<Level, List<ViewPlan>> CreateViewsForLevels(Document doc, List<Level> levels)
        {
            try
            {
                Dictionary<Level, List<ViewPlan>> viewsByLevel = new Dictionary<Level, List<ViewPlan>>();
                
                LoggingService.Log($"Starting to create views for {levels.Count} levels");

                foreach (Level level in levels)
                {
                    try
                    {
                        if (level == null || !level.IsValidObject)
                        {
                            LoggingService.LogWarning("Invalid level encountered, skipping");
                            continue;
                        }

                        LoggingService.Log($"Processing level: {level.Name} (ID: {level.Id.IntegerValue})");
                        List<ViewPlan> levelViews = new List<ViewPlan>();

                        // Create views at different scales
                        foreach (var scaleFactor in _scaleFactors)
                        {
                            try
                            {
                                LoggingService.Log($"Creating view for level {level.Name} at scale {scaleFactor.Key} ({scaleFactor.Value})");
                                
                                ViewPlan view = CreateFloorPlanView(doc, level, scaleFactor.Value);
                                if (view != null && view.IsValidObject)
                                {
                                    LoggingService.Log($"Successfully created view: {view.Name} for level {level.Name}");
                                    levelViews.Add(view);
                                }
                                else
                                {
                                    LoggingService.LogWarning($"Failed to create view for level {level.Name} at scale {scaleFactor.Key}");
                                }
                            }
                            catch (Exception ex)
                            {
                                LoggingService.LogError($"Error creating view for level {level.Name} at scale {scaleFactor.Key}: {ex.Message}");
                                LoggingService.LogError($"Stack trace: {ex.StackTrace}");
                                // Continue with next scale
                            }
                        }

                        if (levelViews.Count > 0)
                        {
                            LoggingService.Log($"Added {levelViews.Count} views for level {level.Name}");
                            viewsByLevel[level] = levelViews;
                        }
                        else
                        {
                            LoggingService.LogWarning($"No views were created for level {level.Name}");
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggingService.LogError($"Error processing level {level?.Name ?? "unknown"}: {ex.Message}");
                        LoggingService.LogError($"Stack trace: {ex.StackTrace}");
                        // Continue with next level
                    }
                }

                LoggingService.Log($"Created a total of {viewsByLevel.Values.Sum(v => v.Count)} views across {viewsByLevel.Count} levels");
                return viewsByLevel;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error in CreateViewsForLevels: {ex.Message}");
                LoggingService.LogError($"Stack trace: {ex.StackTrace}");
                return new Dictionary<Level, List<ViewPlan>>();
            }
        }

        /// <summary>
        /// Creates scope boxes for each level
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="viewsByLevel">The views grouped by level</param>
        /// <returns>A dictionary of levels and their curve loops</returns>
        private static Dictionary<Level, CurveLoop> CreateScopeBoxesForLevels(Document doc, Dictionary<Level, List<ViewPlan>> viewsByLevel)
        {
            Dictionary<Level, CurveLoop> curveLoopsByLevel = new Dictionary<Level, CurveLoop>();

            foreach (var levelViews in viewsByLevel)
            {
                Level level = levelViews.Key;
                List<ViewPlan> views = levelViews.Value;

                if (level == null || !level.IsValidObject || views == null || views.Count == 0)
                {
                    LoggingService.LogWarning("Invalid level or views encountered, skipping curve loop creation");
                    continue;
                }

                // Use the smallest scale view to create the curve loop
                ViewPlan smallestScaleView = views.OrderByDescending(v => v.Scale).FirstOrDefault();
                if (smallestScaleView == null || !smallestScaleView.IsValidObject)
                {
                    LoggingService.LogWarning($"No valid view found for level {level.Name}, skipping curve loop creation");
                    continue;
                }

                // Create a curve loop using RevitScopeBoxService
                CurveLoop curveLoop = RevitScopeBoxService.CreateViewBoundaryForLevel(doc, smallestScaleView, level.Name);
                if (curveLoop != null)
                {
                    curveLoopsByLevel[level] = curveLoop;
                    LoggingService.Log($"Created curve loop for level {level.Name}");
                }
            }

            return curveLoopsByLevel;
        }

        /// <summary>
        /// Applies curve loops to views
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="viewsByLevel">The views grouped by level</param>
        /// <param name="curveLoopsByLevel">The curve loops grouped by level</param>
        private static void ApplyScopeBoxesToViews(Document doc, Dictionary<Level, List<ViewPlan>> viewsByLevel, Dictionary<Level, CurveLoop> curveLoopsByLevel)
        {
            try
            {
                LoggingService.Log("Applying curve loops to views");

                // Get all floor plan views
                FilteredElementCollector collector = new FilteredElementCollector(doc);
                IList<Element> views = collector.OfClass(typeof(ViewPlan))
                    .Where(e => ((ViewPlan)e).ViewType == ViewType.FloorPlan)
                    .ToList();

                foreach (Element viewElement in views)
                {
                    ViewPlan view = viewElement as ViewPlan;
                    if (view == null) continue;

                    string levelName = view.GenLevel?.Name;
                    if (string.IsNullOrEmpty(levelName)) continue;

                    if (curveLoopsByLevel.TryGetValue(view.GenLevel, out CurveLoop curveLoop))
                    {
                        try
                        {
                            LoggingService.Log($"Applying curve loop to view {view.Name}");
                            
                            // Apply the curve loop to the view using RevitScopeBoxService
                            RevitScopeBoxService.ApplyCropRegionToView(doc, view, curveLoop);
                            LoggingService.Log($"Successfully applied curve loop to view {view.Name}");
                        }
                        catch (Exception ex)
                        {
                            LoggingService.LogError($"Error applying curve loop to view: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error in ApplyScopeBoxesToViews: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if a level with the specified name exists
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="levelName">The name of the level</param>
        /// <returns>True if the level exists, false otherwise</returns>
        private static bool LevelExists(Document doc, string levelName)
        {
            try
            {
                LoggingService.Log($"Checking if level '{levelName}' exists");
                
                FilteredElementCollector collector = new FilteredElementCollector(doc);
                IList<Element> levels = collector.OfClass(typeof(Level)).ToElements();
                
                bool exists = levels.Any(e => e.Name == levelName);
                
                if (exists)
                {
                    LoggingService.Log($"Level '{levelName}' exists");
                }
                else
                {
                    LoggingService.Log($"Level '{levelName}' does not exist");
                }
                
                return exists;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error checking if level '{levelName}' exists: {ex.Message}");
                LoggingService.LogError($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Checks if a view exists with the specified criteria
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="viewName">The name of the view to check</param>
        /// <param name="levelName">Optional level name to check</param>
        /// <param name="scale">Optional scale value to check</param>
        /// <returns>True if a view exists matching all specified criteria, false otherwise</returns>
        private static bool ViewExists(Document doc, string viewName, string levelName = null, int? scale = null)
        {
            try
            {
                LoggingService.Log($"Checking if view '{viewName}' exists");
                if (levelName != null)
                {
                    LoggingService.Log($"Level name filter: '{levelName}'");
                }
                if (scale.HasValue)
                {
                    LoggingService.Log($"Scale filter: {scale.Value}");
                }
                
                FilteredElementCollector collector = new FilteredElementCollector(doc);
                IList<Element> views = collector.OfClass(typeof(View)).ToElements();
                
                bool exists = views.Any(e => {
                    if (e.Name != viewName) return false;
                    
                    View view = e as View;
                    if (view == null) return false;
                    
                    if (levelName != null)
                    {
                        // For ViewPlan, we can get the level directly
                        if (view is ViewPlan viewPlan)
                        {
                            if (viewPlan.GenLevel?.Name != levelName) return false;
                        }
                        else
                        {
                            // For other view types, we'll skip the level check since we can't reliably get the level
                            return false;
                        }
                    }
                    
                    if (scale.HasValue)
                    {
                        // Scale is a direct property of View in Revit 2023
                        if (view.Scale != scale.Value) return false;
                    }
                    
                    return true;
                });
                
                if (exists)
                {
                    LoggingService.Log($"View '{viewName}' exists with specified criteria");
                }
                else
                {
                    LoggingService.Log($"View '{viewName}' does not exist with specified criteria");
                }
                
                return exists;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error checking if view '{viewName}' exists: {ex.Message}");
                LoggingService.LogError($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Checks if a view with the specified name and scale already exists for a level
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="levelName">The level name</param>
        /// <param name="scale">The scale value</param>
        /// <returns>True if the view exists, false otherwise</returns>
        private static bool ViewExistsForLevelAndScale(Document doc, string levelName, int scale)
        {
            try
            {
                LoggingService.Log($"Checking if view exists for level '{levelName}' at scale {scale}");
                
                string viewName = $"{levelName} - Scale {scale}";
                bool exists = ViewExists(doc, viewName, levelName, scale);
                
                if (exists)
                {
                    LoggingService.Log($"View '{viewName}' exists for level '{levelName}' at scale {scale}");
                }
                else
                {
                    LoggingService.Log($"View '{viewName}' does not exist for level '{levelName}' at scale {scale}");
                }
                
                return exists;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error checking if view exists for level '{levelName}' at scale {scale}: {ex.Message}");
                LoggingService.LogError($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Gets the floor plan view family type
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <returns>The floor plan view family type</returns>
        private static ViewFamilyType GetFloorPlanViewFamilyType(Document doc)
        {
            try
            {
                LoggingService.Log("Getting floor plan view family type");
                
                // Get all view family types
                FilteredElementCollector collector = new FilteredElementCollector(doc);
                IList<Element> viewFamilyTypes = collector.OfClass(typeof(ViewFamilyType)).ToElements();
                
                LoggingService.Log($"Found {viewFamilyTypes.Count} view family types");
                
                // Find the Floor Plan view family type
                ViewFamilyType floorPlanType = null;
                foreach (Element element in viewFamilyTypes)
                {
                    ViewFamilyType viewType = element as ViewFamilyType;
                    if (viewType != null && viewType.ViewFamily == ViewFamily.FloorPlan)
                    {
                        floorPlanType = viewType;
                        LoggingService.Log($"Found Floor Plan view family type: {floorPlanType.Name}");
                        break;
                    }
                }
                
                if (floorPlanType == null)
                {
                    LoggingService.LogError("Could not find Floor Plan view family type");
                    return null;
                }
                
                return floorPlanType;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error getting floor plan view family type: {ex.Message}");
                LoggingService.LogError($"Stack trace: {ex.StackTrace}");
                return null;
            }
        }
    }
}