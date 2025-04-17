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
            IList<Element> titleblocks = collector.OfClass(typeof(FamilySymbol)).ToElements();

            // Find the titleblock with the specified name
            foreach (Element element in titleblocks)
            {
                FamilySymbol symbol = element as FamilySymbol;
                if (symbol != null && symbol.Family.FamilyCategory.Id.IntegerValue == (int)BuiltInCategory.OST_TitleBlocks)
                {
                    if (symbol.Family.Name == titleblockName)
                    {
                        return symbol.Id;
                    }
                }
            }

            return ElementId.InvalidElementId;
        }
    }
}