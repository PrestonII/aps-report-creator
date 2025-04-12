using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.DB;

namespace ipx.revit.reports.Services
{
    /// <summary>
    /// Service for handling sheet operations in Revit
    /// </summary>
    public class RevitSheetService
    {
        private readonly Document _doc;
        private readonly LoggingService _logger;

        /// <summary>
        /// Initializes a new instance of the RevitSheetService class
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="environment">The environment setting for logging</param>
        public RevitSheetService(Document doc, string environment = "development")
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _logger = new LoggingService(environment);
        }

        /// <summary>
        /// Creates a new sheet in Revit
        /// </summary>
        /// <param name="sheetNumber">Sheet number</param>
        /// <param name="sheetName">Sheet name</param>
        /// <returns>The created sheet</returns>
        public ViewSheet CreateSheet(string sheetNumber, string sheetName)
        {
            _logger.Log($"Creating sheet: {sheetNumber} - {sheetName}");

            // Find a title block
            FamilySymbol titleBlock = FindTitleBlock();
            if (titleBlock == null)
            {
                _logger.LogError("Could not find a title block");
                throw new InvalidOperationException("Could not find a title block");
            }

            // Create the sheet
            ViewSheet sheet = ViewSheet.Create(_doc, titleBlock.Id);
            sheet.SheetNumber = sheetNumber;
            sheet.Name = sheetName;

            _logger.Log($"Sheet created successfully: {sheet.SheetNumber} - {sheet.Name}");
            return sheet;
        }

        /// <summary>
        /// Places a view on a sheet
        /// </summary>
        /// <param name="sheet">The sheet</param>
        /// <param name="view">The view to place</param>
        /// <param name="position">The position to place the view</param>
        /// <returns>The created viewport</returns>
        public Viewport PlaceViewOnSheet(ViewSheet sheet, View view, XYZ position)
        {
            _logger.Log($"Placing view {view.Name} on sheet {sheet.Name}");

            // Check if the view can be placed on a sheet
            if (!view.ViewType.ToString().Contains("Drafting"))
            {
                _logger.LogError($"View {view.Name} cannot be placed on a sheet");
                throw new InvalidOperationException($"View {view.Name} cannot be placed on a sheet");
            }

            // Create the viewport
            Viewport viewport = Viewport.Create(_doc, sheet.Id, view.Id, position);

            _logger.Log("View placed successfully on sheet");
            return viewport;
        }

        /// <summary>
        /// Finds a title block in the document
        /// </summary>
        /// <returns>A title block family symbol</returns>
        private FamilySymbol FindTitleBlock()
        {
            // Get all title block types
            var titleBlocks = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .OfClass(typeof(FamilySymbol))
                .FirstOrDefault(e => e is FamilySymbol) as FamilySymbol;

            if (titleBlocks == null)
            {
                _logger.LogWarning("No title blocks found in the document");
                return null;
            }

            // Ensure the symbol is active
            if (!titleBlocks.IsActive)
            {
                titleBlocks.Activate();
            }

            return titleBlocks;
        }

        /// <summary>
        /// Gets all sheets in the document
        /// </summary>
        /// <returns>List of sheets</returns>
        public List<ViewSheet> GetAllSheets()
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewSheet))
                .WhereElementIsNotElementType()
                .Cast<ViewSheet>()
                .ToList();
        }

        /// <summary>
        /// Gets a sheet by number
        /// </summary>
        /// <param name="sheetNumber">The sheet number to find</param>
        /// <returns>The sheet, or null if not found</returns>
        public ViewSheet GetSheetByNumber(string sheetNumber)
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewSheet))
                .WhereElementIsNotElementType()
                .Cast<ViewSheet>()
                .FirstOrDefault(s => s.SheetNumber == sheetNumber);
        }
    }
}