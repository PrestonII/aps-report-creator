using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ipx.revit.reports.Utilities
{
    /// <summary>
    /// Utility class for level-related operations that can be tested independently of Revit
    /// </summary>
    public static class LevelUtility
    {
        /// <summary>
        /// Extracts a numeric value from a level name for sorting
        /// </summary>
        /// <param name="levelName">The level name</param>
        /// <returns>A numeric value for sorting, or 0 if no number found</returns>
        public static int ExtractLevelNumber(string levelName)
        {
            if (string.IsNullOrEmpty(levelName))
                return 0;

            // Extract the first number found in the level name
            string numberString = new string(levelName.Where(c => char.IsDigit(c)).ToArray());
            
            if (int.TryParse(numberString, out int number))
            {
                return number;
            }
            
            return 0;
        }

        /// <summary>
        /// Groups levels for combined sheets (2-4 levels per sheet)
        /// </summary>
        /// <param name="sortedLevels">The sorted list of level names</param>
        /// <returns>Groups of levels for combined sheets</returns>
        public static List<List<string>> GroupLevelsForCombinedSheets(List<string> sortedLevels)
        {
            if (sortedLevels == null || !sortedLevels.Any())
                return new List<List<string>>();

            List<List<string>> levelGroups = new List<List<string>>();
            
            for (int i = 0; i < sortedLevels.Count; i += 4)
            {
                List<string> group = new List<string>();
                
                // Add up to 4 levels to the group
                for (int j = 0; j < 4 && i + j < sortedLevels.Count; j++)
                {
                    group.Add(sortedLevels[i + j]);
                }
                
                levelGroups.Add(group);
            }
            
            return levelGroups;
        }

        /// <summary>
        /// Sorts levels by their numeric value
        /// </summary>
        /// <param name="levels">The list of level names to sort</param>
        /// <returns>A sorted list of level names</returns>
        public static List<string> SortLevelsByNumber(List<string> levels)
        {
            if (levels == null || !levels.Any())
                return new List<string>();

            return levels
                .OrderBy(l => ExtractLevelNumber(l))
                .ToList();
        }
    }
} 