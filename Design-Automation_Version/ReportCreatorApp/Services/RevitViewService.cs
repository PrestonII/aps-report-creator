using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.DB;

namespace ipx.revit.reports.Services
{
    public static class RevitViewService
    {
        // Define the scales we need to create
        private static readonly Dictionary<string, double> Scales = new Dictionary<string, double>
        {
            { "1/4\" = 1'-0\"", 1.0 / 48.0 },
            { "3/16\" = 1'-0\"", 1.0 / 64.0 },
            { "1/8\" = 1'-0\"", 1.0 / 96.0 },
            { "3/32\" = 1'-0\"", 1.0 / 128.0 },
            { "1/16\" = 1'-0\"", 1.0 / 192.0 },
            { "1:10", 1.0 / 10.0 },
            { "1:20", 1.0 / 20.0 },
            { "1:100", 1.0 / 100.0 },
            { "1:125", 1.0 / 125.0 },
            { "1:150", 1.0 / 150.0 },
            { "1:200", 1.0 / 200.0 },
            { "1:250", 1.0 / 250.0 }
        };

        /// <summary>
        /// Creates levels and floor plan views based on the levels in the linked files
        /// </summary>
        /// <param name="doc">The host document</param>
        /// <returns>The number of views created</returns>
        public static int CreateLevelsAndViews(Document doc)
        {
            try
            {
                LoggingService.Log("Starting to create levels and views...");

                // Get all RevitLinkInstance elements
                FilteredElementCollector collector = new FilteredElementCollector(doc);
                IList<Element> linkInstances = collector.OfClass(typeof(RevitLinkInstance)).ToElements();

                if (linkInstances.Count == 0)
                {
                    LoggingService.LogError("No Revit links found in the document");
                    return 0;
                }

                // Dictionary to store level names and their elevations
                Dictionary<string, double> levelElevations = new Dictionary<string, double>();
                
                // Dictionary to store level name conflicts
                Dictionary<string, int> levelNameConflicts = new Dictionary<string, int>();

                // Collect level data from all linked files
                foreach (Element linkElement in linkInstances)
                {
                    RevitLinkInstance linkInstance = linkElement as RevitLinkInstance;
                    if (linkInstance != null)
                    {
                        Document linkDoc = linkInstance.GetLinkDocument();
                        if (linkDoc != null)
                        {
                            // Get the link name to use as a prefix if needed
                            string linkName = linkInstance.GetLinkDocument().Title;
                            if (string.IsNullOrEmpty(linkName))
                            {
                                linkName = "Link";
                            }

                            // Get all levels from the linked document
                            FilteredElementCollector levelCollector = new FilteredElementCollector(linkDoc);
                            IList<Element> levels = levelCollector.OfClass(typeof(Level)).ToElements();

                            foreach (Element levelElement in levels)
                            {
                                Level level = levelElement as Level;
                                if (level != null)
                                {
                                    string levelName = level.Name;
                                    double elevation = level.Elevation;

                                    // Check if we already have a level with this name
                                    if (levelElevations.ContainsKey(levelName))
                                    {
                                        // If the elevation is different, we need to handle the conflict
                                        if (Math.Abs(levelElevations[levelName] - elevation) > 0.001)
                                        {
                                            // Increment the conflict counter for this level name
                                            if (!levelNameConflicts.ContainsKey(levelName))
                                            {
                                                levelNameConflicts[levelName] = 1;
                                            }
                                            else
                                            {
                                                levelNameConflicts[levelName]++;
                                            }

                                            // Create a new level name with a suffix
                                            string newLevelName = $"{levelName}{GetLevelSuffix(levelNameConflicts[levelName])}";
                                            levelElevations[newLevelName] = elevation;
                                            LoggingService.Log($"Renamed conflicting level '{levelName}' to '{newLevelName}'");
                                        }
                                    }
                                    else
                                    {
                                        levelElevations[levelName] = elevation;
                                    }
                                }
                            }
                        }
                    }
                }

                // Sort levels by elevation
                var sortedLevels = levelElevations.OrderBy(kvp => kvp.Value).ToList();

                // Create levels and views
                int viewCount = 0;
                using (Transaction tx = new Transaction(doc, "Create Levels and Views"))
                {
                    tx.Start();

                    // Create levels
                    Dictionary<string, Level> createdLevels = new Dictionary<string, Level>();
                    foreach (var levelInfo in sortedLevels)
                    {
                        string levelName = levelInfo.Key;
                        double elevation = levelInfo.Value;

                        // Create the level
                        Level level = Level.Create(doc, elevation);
                        level.Name = levelName;
                        createdLevels[levelName] = level;
                        LoggingService.Log($"Created level: {levelName} at elevation {elevation}");
                    }

                    // Create floor plan views for each level at each scale
                    foreach (var levelInfo in sortedLevels)
                    {
                        string levelName = levelInfo.Key;
                        Level level = createdLevels[levelName];

                        foreach (var scale in Scales)
                        {
                            string scaleName = scale.Key;
                            double scaleFactor = scale.Value;

                            // Create the floor plan view
                            ViewFamilyType viewFamilyType = GetFloorPlanViewFamilyType(doc);
                            ViewPlan viewPlan = ViewPlan.Create(doc, viewFamilyType.Id, level.Id);
                            
                            // Round the scale factor to the nearest whole number
                            int roundedScale = (int)Math.Round(1.0 / scaleFactor);
                            
                            // Use a format without prohibited characters
                            viewPlan.Name = $"{levelName} - Scale {roundedScale}";
                            viewPlan.Scale = roundedScale;
                            LoggingService.Log($"Created floor plan view: {viewPlan.Name} at scale {roundedScale}");
                            viewCount++;
                        }
                    }

                    tx.Commit();
                }

                LoggingService.Log($"Successfully created {viewCount} views");
                return viewCount;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error creating levels and views: {ex.Message}");
                throw ex;
            }
        }

        /// <summary>
        /// Gets a suffix for a level name based on the conflict count
        /// </summary>
        /// <param name="conflictCount">The number of conflicts for this level name</param>
        /// <returns>A suffix string (A, B, C, etc.)</returns>
        private static string GetLevelSuffix(int conflictCount)
        {
            // Convert the conflict count to a letter (A, B, C, etc.)
            return ((char)('A' + conflictCount - 1)).ToString();
        }

        /// <summary>
        /// Gets the ViewFamilyType for floor plans
        /// </summary>
        /// <param name="doc">The document</param>
        /// <returns>The ViewFamilyType for floor plans</returns>
        private static ViewFamilyType GetFloorPlanViewFamilyType(Document doc)
        {
            // Get all ViewFamilyTypes
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            IList<Element> viewFamilyTypes = collector.OfClass(typeof(ViewFamilyType)).ToElements();

            // Find the first ViewFamilyType for floor plans
            foreach (Element element in viewFamilyTypes)
            {
                ViewFamilyType viewFamilyType = element as ViewFamilyType;
                if (viewFamilyType != null && viewFamilyType.ViewFamily == ViewFamily.FloorPlan)
                {
                    return viewFamilyType;
                }
            }

            // If no floor plan ViewFamilyType is found, throw an exception
            throw new Exception("No floor plan ViewFamilyType found in the document");
        }
    }
} 