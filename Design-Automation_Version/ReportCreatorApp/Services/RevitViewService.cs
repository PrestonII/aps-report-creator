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
                Dictionary<Level, Element> scopeBoxesByLevel = CreateScopeBoxesForLevels(doc, viewsByLevel);

                // STEP 5: Apply scope boxes to all views for each level
                ApplyScopeBoxesToViews(doc, viewsByLevel, scopeBoxesByLevel);

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
            Dictionary<Level, List<ViewPlan>> viewsByLevel = new Dictionary<Level, List<ViewPlan>>();
            
            LoggingService.Log($"Starting to create views for {levels.Count} levels");

            foreach (Level level in levels)
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

            LoggingService.Log($"Created a total of {viewsByLevel.Values.Sum(v => v.Count)} views across {viewsByLevel.Count} levels");
            return viewsByLevel;
        }

        /// <summary>
        /// Creates scope boxes for each level
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="viewsByLevel">The views grouped by level</param>
        /// <returns>A dictionary of levels and their scope boxes</returns>
        private static Dictionary<Level, Element> CreateScopeBoxesForLevels(Document doc, Dictionary<Level, List<ViewPlan>> viewsByLevel)
        {
            Dictionary<Level, Element> scopeBoxesByLevel = new Dictionary<Level, Element>();

            foreach (var levelViews in viewsByLevel)
            {
                Level level = levelViews.Key;
                List<ViewPlan> views = levelViews.Value;

                if (level == null || !level.IsValidObject || views == null || views.Count == 0)
                {
                    LoggingService.LogWarning("Invalid level or views encountered, skipping scope box creation");
                    continue;
                }

                // Use the smallest scale view to create the scope box
                ViewPlan smallestScaleView = views.OrderByDescending(v => v.Scale).FirstOrDefault();
                if (smallestScaleView == null || !smallestScaleView.IsValidObject)
                {
                    LoggingService.LogWarning($"No valid view found for level {level.Name}, skipping scope box creation");
                    continue;
                }

                // Create a scope box using RevitScopeBoxService
                Element scopeBox = RevitScopeBoxService.CreateViewBoundaryForLevel(doc, smallestScaleView, level.Name);
                if (scopeBox != null && scopeBox.IsValidObject)
                {
                    scopeBoxesByLevel[level] = scopeBox;
                }
            }

            return scopeBoxesByLevel;
        }

        /// <summary>
        /// Applies scope boxes to views
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="viewsByLevel">The views grouped by level</param>
        /// <param name="scopeBoxesByLevel">The scope boxes grouped by level</param>
        private static void ApplyScopeBoxesToViews(Document doc, Dictionary<Level, List<ViewPlan>> viewsByLevel, Dictionary<Level, Element> scopeBoxesByLevel)
        {
            using (Transaction tx = new Transaction(doc, "Apply Scope Boxes"))
            {
                tx.Start();
                try
                {
                    foreach (var levelViews in viewsByLevel)
                    {
                        Level level = levelViews.Key;
                        List<ViewPlan> views = levelViews.Value;

                        if (level == null || !level.IsValidObject || views == null || views.Count == 0)
                            continue;

                        if (scopeBoxesByLevel.TryGetValue(level, out Element scopeBox) && scopeBox != null && scopeBox.IsValidObject)
                        {
                            foreach (ViewPlan view in views)
                            {
                                if (view != null && view.IsValidObject)
                                {
                                    // Set the view's crop box to match the boundary
                                    view.CropBox = scopeBox.get_BoundingBox(null);
                                    view.CropBoxVisible = true;
                                    
                                    // In Revit 2023, we don't need to set a scope box parameter
                                    // The crop box is sufficient for controlling the view boundary
                                    LoggingService.LogDebug($"Set crop box for view {view.Name}");
                                }
                            }
                        }
                    }
                    tx.Commit();
                }
                catch (Exception ex)
                {
                    tx.RollBack();
                    LoggingService.LogError($"Error applying scope boxes: {ex.Message}");
                }
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
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            IList<Element> levels = collector.OfClass(typeof(Level)).ToElements();
            
            return levels.Any(e => e.Name == levelName);
        }

        /// <summary>
        /// Checks if a view with the specified name exists
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="viewName">The name of the view</param>
        /// <returns>True if the view exists, false otherwise</returns>
        private static bool ViewExists(Document doc, string viewName)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            IList<Element> views = collector.OfClass(typeof(View)).ToElements();
            
            return views.Any(e => e.Name == viewName);
        }

        /// <summary>
        /// Checks if a view with the specified name and scale already exists
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="levelName">The level name</param>
        /// <param name="scale">The scale value</param>
        /// <returns>True if the view exists, false otherwise</returns>
        private static bool ViewExistsForLevelAndScale(Document doc, string levelName, int scale)
        {
            string viewName = $"{levelName} - Scale {scale}";
            
            return ViewExists(doc, viewName);
        }

        /// <summary>
        /// Gets the floor plan view family type
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <returns>The floor plan view family type</returns>
        private static ViewFamilyType GetFloorPlanViewFamilyType(Document doc)
        {
            // Get all view family types
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            IList<Element> viewFamilyTypes = collector.OfClass(typeof(ViewFamilyType)).ToElements();
            
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
    }
}