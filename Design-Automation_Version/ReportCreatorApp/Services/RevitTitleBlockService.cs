using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.DB;

namespace ipx.revit.reports.Services
{
    /// <summary>
    /// Service for handling title block operations in Revit
    /// </summary>
    public static class RevitTitleBlockService
    {
        /// <summary>
        /// Gets the ElementId of a titleblock family by name
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="titleblockName">The name of the titleblock family</param>
        /// <returns>The ElementId of the titleblock family, or ElementId.InvalidElementId if not found</returns>
        public static ElementId GetTitleblockId(Document doc, string titleblockName)
        {
            // Get all titleblock families
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            IList<Element> titleblocks = collector
                .OfClass(typeof(FamilySymbol))
                .Where(e => (e as FamilySymbol).Category.BuiltInCategory == BuiltInCategory.OST_TitleBlocks)
                .ToList();

            // Find the titleblock with the specified name
            foreach (Element element in titleblocks)
            {
                if (String.Equals((element as FamilySymbol).FamilyName, titleblockName))
                {
                    return element.Id;
                }
            }

            return ElementId.InvalidElementId;
        }
    }
}