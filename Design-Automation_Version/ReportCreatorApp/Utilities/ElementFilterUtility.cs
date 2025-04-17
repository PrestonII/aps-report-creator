using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.DB;

namespace ipx.revit.reports.Utilities
{
    /// <summary>
    /// Utility class for creating element filters
    /// </summary>
    public static class ElementFilterUtility
    {
        /// <summary>
        /// Creates a filter for model elements (walls, roofs, floors)
        /// </summary>
        /// <returns>A logical OR filter for model elements</returns>
        public static LogicalOrFilter CreateModelElementFilter()
        {
            ElementClassFilter wallFilter = new ElementClassFilter(typeof(Wall));
            ElementClassFilter roofFilter = new ElementClassFilter(typeof(RoofBase));
            ElementClassFilter floorFilter = new ElementClassFilter(typeof(Floor));

            LogicalOrFilter orFilter = new LogicalOrFilter(wallFilter, roofFilter);
            return new LogicalOrFilter(orFilter, floorFilter);
        }

        /// <summary>
        /// Creates a filter for elements at a specific level
        /// </summary>
        /// <param name="levelId">The ID of the level</param>
        /// <returns>A filter for elements at the specified level</returns>
        public static ElementLevelFilter CreateLevelFilter(ElementId levelId)
        {
            return new ElementLevelFilter(levelId);
        }

        /// <summary>
        /// Creates a combined filter for model elements at a specific level
        /// </summary>
        /// <param name="levelId">The ID of the level</param>
        /// <returns>A combined filter for model elements at the specified level</returns>
        public static LogicalAndFilter CreateModelElementsAtLevelFilter(ElementId levelId)
        {
            LogicalOrFilter modelFilter = CreateModelElementFilter();
            ElementLevelFilter levelFilter = CreateLevelFilter(levelId);
            return new LogicalAndFilter(modelFilter, levelFilter);
        }
    }
} 