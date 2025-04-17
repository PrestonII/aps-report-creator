using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.DB;

namespace ipx.revit.reports.Services
{
    /// <summary>
    /// Service for handling level operations in Revit
    /// </summary>
    public static class RevitLevelService
    {
        /// <summary>
        /// Collects levels from linked files
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="revitLinks">The linked Revit files</param>
        /// <returns>A list of levels</returns>
        public static List<Level> CollectLevelsFromLinkedFiles(Document doc, IList<Element> revitLinks)
        {
            List<Level> levels = new List<Level>();
            
            foreach (Element link in revitLinks)
            {
                RevitLinkInstance linkInstance = link as RevitLinkInstance;
                if (linkInstance == null) continue;

                Document linkDoc = linkInstance.GetLinkDocument();
                if (linkDoc == null) continue;

                // Get levels from the linked document
                FilteredElementCollector collector = new FilteredElementCollector(linkDoc);
                IList<Element> linkLevels = collector.OfClass(typeof(Level)).ToElements();

                foreach (Element level in linkLevels)
                {
                    Level revitLevel = level as Level;
                    if (revitLevel != null)
                    {
                        levels.Add(revitLevel);
                    }
                }
            }

            return levels;
        }

        /// <summary>
        /// Creates levels in the current document
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="levels">The levels to create</param>
        /// <returns>A list of created levels</returns>
        public static List<Level> CreateLevels(Document doc, List<Level> levels)
        {
            List<Level> createdLevels = new List<Level>();

            using (Transaction tx = new Transaction(doc, "Create Levels"))
            {
                tx.Start();

                foreach (Level level in levels)
                {
                    // Check if level already exists
                    if (LevelExists(doc, level.Name))
                    {
                        LoggingService.Log($"Level {level.Name} already exists, skipping creation");
                        continue;
                    }

                    // Create the level
                    Level newLevel = Level.Create(doc, level.Elevation);
                    newLevel.Name = level.Name;

                    createdLevels.Add(newLevel);
                }

                tx.Commit();
            }

            return createdLevels;
        }

        /// <summary>
        /// Checks if a level exists in the document
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="levelName">The level name</param>
        /// <returns>True if the level exists, false otherwise</returns>
        public static bool LevelExists(Document doc, string levelName)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            IList<Element> levels = collector.OfClass(typeof(Level)).ToElements();

            return levels.Any(e => e.Name == levelName);
        }
        
        /// <summary>
        /// Finds a level by name in the document
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="levelName">The level name</param>
        /// <returns>The level if found, null otherwise</returns>
        public static Level FindLevelByName(Document doc, string levelName)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            IList<Element> levels = collector
                .OfClass(typeof(Level))
                .WhereElementIsNotElementType()
                .ToElements();

            return levels.FirstOrDefault(e => e.Name == levelName) as Level;
        }
        
        /// <summary>
        /// Finds the corresponding level in a linked document
        /// </summary>
        /// <param name="linkDoc">The linked document</param>
        /// <param name="mainLevel">The level in the main document</param>
        /// <returns>The corresponding level in the linked document, or null if not found</returns>
        public static Level FindCorrespondingLevel(Document linkDoc, Level mainLevel)
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