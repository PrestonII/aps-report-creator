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
        private static readonly Dictionary<string, int> _scaleFactors = new Dictionary<string, int>
        {
            { "1/4\" = 1'-0\"", 48 },
            { "3/16\" = 1'-0\"", 64 },
            { "1/8\" = 1'-0\"", 96 },
            { "3/32\" = 1'-0\"", 128 },
            { "1/16\" = 1'-0\"", 192 },
            { "3/64\" = 1'-0\"", 256 },
            { "1/32\" = 1'-0\"", 384 },
            { "3/128\" = 1'-0\"", 512 },
            { "1/64\" = 1'-0\"", 768 },
            { "1:100", 100 },
            { "1:200", 200 },
            { "1:250", 250 }
        };

        /// <summary>
        /// Creates levels and views based on the linked files
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <returns>The number of views created</returns>
        public static int CreateLevelsAndViews(Document doc)
        {
            try
            {
                LoggingService.Log("Starting to create levels and views...");

                // Get all linked documents
                FilteredElementCollector collectorLinks = new FilteredElementCollector(doc);
                IList<Element> revitLinks = collectorLinks.OfClass(typeof(RevitLinkInstance)).ToElements();

                if (revitLinks.Count == 0)
                {
                    LoggingService.LogError("No linked Revit files found");
                    return 0;
                }

                LoggingService.Log($"Found {revitLinks.Count} linked Revit files");

                // Collect all levels from linked files
                List<Level> levels = new List<Level>();
                foreach (Element linkElement in revitLinks)
                {
                    RevitLinkInstance linkInstance = linkElement as RevitLinkInstance;
                    Document linkDoc = linkInstance.GetLinkDocument();

                    if (linkDoc != null)
                    {
                        // Get all levels from the linked document
                        FilteredElementCollector levelCollector = new FilteredElementCollector(linkDoc);
                        IList<Element> linkLevels = levelCollector.OfClass(typeof(Level)).ToElements();

                        foreach (Element levelElement in linkLevels)
                        {
                            Level level = levelElement as Level;
                            if (level != null)
                            {
                                levels.Add(level);
                            }
                        }
                    }
                }

                if (levels.Count == 0)
                {
                    LoggingService.LogError("No levels found in linked files");
                    return 0;
                }

                LoggingService.Log($"Found {levels.Count} levels in linked files");

                // Create levels in the current document
                List<Level> createdLevels = CreateLevels(doc, levels);
                LoggingService.Log($"Created {createdLevels.Count} levels");

                // Create floor plan views for each level at each scale
                int viewCount = 0;
                foreach (var level in createdLevels)
                {
                    string levelName = level.Name;

                    // TODO: Scope box functionality temporarily disabled
                    /*
                    // Create a temporary view to create the boundary in
                    View tempView = null;
                    using (Transaction tx = new Transaction(doc, "Create Temporary View"))
                    {
                        tx.Start();

                        // First find a non-template floor plan view to use as reference
                        FilteredElementCollector collectorPlans = new FilteredElementCollector(doc);
                        View referenceView = collectorPlans
                            .OfClass(typeof(View))
                            .Cast<View>()
                            .FirstOrDefault(v => v is ViewPlan && !v.IsTemplate);

                        if (referenceView != null)
                        {
                            // Get the view type from the reference view
                            ViewFamilyType viewType = doc.GetElement(referenceView.GetTypeId()) as ViewFamilyType;
                            if (viewType != null)
                            {
                                // Create a temporary view
                                tempView = ViewPlan.Create(doc, viewType.Id, level.Id);
                                tempView.Name = $"Temporary View - {levelName}";
                            }
                        }

                        tx.Commit();
                    }

                    if (tempView == null)
                    {
                        LoggingService.LogWarning($"Could not create temporary view for level {levelName}");
                        continue;
                    }

                    // Create a view boundary for the level
                    Element boundaryElement = RevitScopeBoxService.CreateViewBoundaryForLevel(doc, tempView, levelName);

                    if (boundaryElement == null)
                    {
                        LoggingService.LogWarning($"Could not create view boundary for level {levelName}");
                        continue;
                    }
                    */

                    // Create floor plan views for each scale
                    foreach (var scale in _scaleFactors)
                    {
                        string viewName = $"{levelName} - Scale {scale.Key}";

                        // Check if a view with this name already exists
                        if (ViewExists(doc, viewName))
                        {
                            LoggingService.LogWarning($"View {viewName} already exists, skipping");
                            continue;
                        }

                        // Create the floor plan view
                        ViewPlan view = CreateFloorPlanView(doc, level, scale.Value);

                        if (view != null)
                        {
                            // TODO: Scope box functionality temporarily disabled
                            /*
                            // Apply the view boundary to the view
                            RevitScopeBoxService.ApplyViewBoundary(doc, view, boundaryElement);
                            */

                            LoggingService.Log($"Created view {viewName}");
                            viewCount++;
                        }
                    }

                    // TODO: Scope box functionality temporarily disabled
                    /*
                    // Delete the temporary view
                    using (Transaction tx = new Transaction(doc, "Delete Temporary View"))
                    {
                        tx.Start();
                        doc.Delete(tempView.Id);
                        tx.Commit();
                    }
                    */
                }

                LoggingService.Log($"Created {viewCount} views");
                return viewCount;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error creating levels and views: {ex.Message}");
                throw ex;
            }
        }

        /// <summary>
        /// Creates levels in the current document based on levels from linked files
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="levels">The levels to create</param>
        /// <returns>The created levels</returns>
        private static List<Level> CreateLevels(Document doc, List<Level> levels)
        {
            List<Level> createdLevels = new List<Level>();
            Dictionary<string, int> levelNameCount = new Dictionary<string, int>();

            using (Transaction tx = new Transaction(doc, "Create Levels"))
            {
                tx.Start();

                foreach (Level level in levels)
                {
                    string levelName = level.Name;
                    double elevation = level.Elevation;

                    // Check if a level with this name already exists
                    if (LevelExists(doc, levelName))
                    {
                        // Append a letter to the level name
                        int count = 1;
                        string newLevelName = $"{levelName} A";
                        while (LevelExists(doc, newLevelName))
                        {
                            count++;
                            newLevelName = $"{levelName} {((char)('A' + count - 1)).ToString()}";
                        }
                        levelName = newLevelName;
                    }

                    // Create the level
                    Level newLevel = Level.Create(doc, elevation);
                    newLevel.Name = levelName;
                    createdLevels.Add(newLevel);
                }

                tx.Commit();
            }

            return createdLevels;
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
                // First find a non-template floor plan view to use as reference
                FilteredElementCollector collector = new FilteredElementCollector(doc);
                View referenceView = collector
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .FirstOrDefault(v => v is ViewPlan && !v.IsTemplate);

                if (referenceView == null)
                {
                    LoggingService.LogError("Could not find a non-template floor plan view to use as reference");
                    return null;
                }

                // Get the view type from the reference view
                ViewFamilyType viewType = doc.GetElement(referenceView.GetTypeId()) as ViewFamilyType;
                if (viewType == null)
                {
                    LoggingService.LogError("Could not get view type from reference view");
                    return null;
                }

                // Create the floor plan view
                ViewPlan view = ViewPlan.Create(doc, viewType.Id, level.Id);
                if (view == null)
                {
                    LoggingService.LogError("Failed to create floor plan view");
                    return null;
                }

                // Set the scale
                view.Scale = scale;

                return view;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error creating floor plan view: {ex.Message}");
                return null;
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
    }
}