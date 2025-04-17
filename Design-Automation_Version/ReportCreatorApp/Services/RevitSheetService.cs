using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.DB;

using ipx.revit.reports.Models;
using ipx.revit.reports.Utilities;

namespace ipx.revit.reports.Services
{
    /// <summary>
    /// Service for handling sheet operations in Revit
    /// </summary>
    public static class RevitSheetService
    {
        // Constants for sheet dimensions (in feet)
        private const double SHEET_WIDTH = 8.5; // Letter size width
        private const double SHEET_HEIGHT = 11.0; // Letter size height

        // Constants for individual view placement
        private const double INDIVIDUAL_VIEW_MAX_WIDTH = 10.5 / 12.0; // 10.5 inches in feet
        private const double INDIVIDUAL_VIEW_MAX_HEIGHT = 7.25 / 12.0; // 7.25 inches in feet

        // Constants for 2-panel sheet
        private const double TWO_PANEL_VIEW_WIDTH = 5.125 / 12.0; // 5.125 inches in feet
        private const double TWO_PANEL_VIEW_HEIGHT = 7.25 / 12.0; // 7.25 inches in feet
        private const double PANEL_OFFSET = 0.25 / 12.0; // 0.25 inches in feet

        // Constants for 4-panel sheet
        private const double FOUR_PANEL_VIEW_WIDTH = 5.125 / 12.0; // 5.125 inches in feet
        private const double FOUR_PANEL_VIEW_HEIGHT = 3.5 / 12.0; // 3.5 inches in feet

        /// <summary>
        /// Places views on sheets
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="views">The views to place</param>
        /// <param name="sheets">The sheets to place views on</param>
        /// <returns>The number of views placed</returns>
        public static int PlaceViewsOnSheets(Document doc, IList<ViewPlan> views, IList<ViewSheet> sheets)
        {
            try
            {
                LoggingService.Log($"Starting to place {views.Count} views on {sheets.Count} sheets...");

                // Filter out template views and ensure they're valid ViewPlan objects
                IList<ViewPlan> nonTemplateViews = views.Where(v =>
                    v != null &&
                    v.IsValidObject &&
                    !v.IsTemplate
                ).ToList();

                LoggingService.Log($"Found {nonTemplateViews.Count} valid non-template views to place");

                if (nonTemplateViews.Count == 0)
                {
                    LoggingService.LogWarning("No valid non-template views to place on sheets");
                    return 0;
                }

                if (sheets.Count == 0)
                {
                    LoggingService.LogWarning("No sheets to place views on");
                    return 0;
                }

                int viewsPlaced = 0;

                // Place views on sheets
                foreach (ViewSheet sheet in sheets)
                {
                    if (sheet == null || !sheet.IsValidObject)
                    {
                        LoggingService.LogWarning("Invalid sheet encountered, skipping");
                        continue;
                    }

                    // Get the titleblock
                    ElementId titleblockId = RevitTitleBlockService.GetTitleblockId(doc, "_SCHEMATIC PLAN TITLEBLOCK");
                    if (titleblockId == ElementId.InvalidElementId)
                    {
                        LoggingService.LogWarning($"Could not find titleblock for sheet {sheet.Name}");
                        continue;
                    }

                    // Get the titleblock outline
                    Outline titleblockOutline = GetTitleblockOutline(doc, titleblockId);
                    if (titleblockOutline == null)
                    {
                        LoggingService.LogWarning($"Could not get titleblock outline for sheet {sheet.Name}");
                        continue;
                    }

                    // Calculate the available area for views
                    double availableWidth = titleblockOutline.MaximumPoint.X - titleblockOutline.MinimumPoint.X - 2.0; // 2 feet margin
                    double availableHeight = titleblockOutline.MaximumPoint.Y - titleblockOutline.MinimumPoint.Y - 2.0; // 2 feet margin

                    // Calculate the number of views that can fit on the sheet
                    int maxViewsPerSheet = (int)(availableWidth * availableHeight / (4.0 * 3.0)); // Assuming each view takes 4x3 feet
                    maxViewsPerSheet = Math.Max(1, maxViewsPerSheet); // At least 1 view per sheet

                    // Place views on the sheet
                    int viewsOnSheet = 0;
                    double x = titleblockOutline.MinimumPoint.X + 1.0; // 1 foot margin
                    double y = titleblockOutline.MinimumPoint.Y + 1.0; // 1 foot margin

                    foreach (ViewPlan view in nonTemplateViews)
                    {
                        if (viewsOnSheet >= maxViewsPerSheet)
                            break;

                        if (view == null || !view.IsValidObject)
                        {
                            LoggingService.LogWarning($"Invalid view encountered, skipping");
                            continue;
                        }

                        // Convert ViewPlan to IPXView
                        IPXView ipxView = RevitViewConverter.ConvertToIPXView(view);
                        if (ipxView == null)
                        {
                            LoggingService.LogWarning($"Could not convert view {view.Name} to IPXView, skipping");
                            continue;
                        }

                        // Place the view on the sheet
                        XYZ viewCenter = new XYZ(x, y, 0);
                        Viewport viewport = PlaceViewOnSheet(doc, sheet, ipxView, viewCenter);

                        if (viewport != null)
                        {
                            // Move to the next position
                            x += 4.0; // 4 feet width
                            if (x + 4.0 > titleblockOutline.MaximumPoint.X - 1.0)
                            {
                                x = titleblockOutline.MinimumPoint.X + 1.0;
                                y += 3.0; // 3 feet height
                            }

                            viewsOnSheet++;
                            viewsPlaced++;
                        }
                    }

                    LoggingService.Log($"Placed {viewsOnSheet} views on sheet {sheet.Name}");
                }

                LoggingService.Log($"Placed {viewsPlaced} views on {sheets.Count} sheets");
                return viewsPlaced;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error placing views on sheets: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Creates individual sheets for each level
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="viewsByLevel">The views grouped by level</param>
        /// <returns>The number of individual sheets created</returns>
        private static int CreateIndividualSheets(Document doc, Dictionary<string, List<IPXView>> viewsByLevel)
        {
            int sheetCount = 0;

            // Get the titleblock family
            ElementId titleblockId = RevitTitleBlockService.GetTitleblockId(doc, "_SCHEMATIC PLAN TITLEBLOCK");
            if (titleblockId == ElementId.InvalidElementId)
            {
                LoggingService.LogError("Could not find the '_SCHEMATIC PLAN TITLEBLOCK' titleblock");
                return 0;
            }

            using (Transaction tx = new Transaction(doc, "Create Individual Sheets"))
            {
                tx.Start();

                foreach (var levelViews in viewsByLevel)
                {
                    string levelName = levelViews.Key;
                    List<IPXView> views = levelViews.Value;

                    // Find the largest view that fits within the individual view constraints
                    IPXView bestView = ViewService.FindBestFittingView(views, INDIVIDUAL_VIEW_MAX_WIDTH, INDIVIDUAL_VIEW_MAX_HEIGHT);

                    if (bestView != null)
                    {
                        // Get the ElementId of the view
                        ElementId viewId = RevitViewConverter.GetElementId(bestView);

                        if (viewId != ElementId.InvalidElementId)
                        {
                            // Create a sheet
                            ViewSheet sheet = ViewSheet.Create(doc, titleblockId);
                            sheet.Name = $"Individual - {levelName}";

                            // Place the view on the sheet
                            XYZ viewCenter = new XYZ(SHEET_WIDTH / 2, SHEET_HEIGHT / 2, 0);
                            PlaceViewOnSheet(doc, sheet, bestView, viewCenter);

                            LoggingService.Log($"Created individual sheet for level {levelName} with view {bestView.Name}");
                            sheetCount++;
                        }
                    }
                    else
                    {
                        LoggingService.LogWarning($"No suitable view found for individual sheet for level {levelName}");
                    }
                }

                tx.Commit();
            }

            return sheetCount;
        }

        /// <summary>
        /// Creates combined sheets for multiple levels
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="viewsByLevel">The views grouped by level</param>
        /// <returns>The number of combined sheets created</returns>
        private static int CreateCombinedSheets(Document doc, Dictionary<string, List<IPXView>> viewsByLevel)
        {
            int sheetCount = 0;

            // Get the titleblock family
            ElementId titleblockId = RevitTitleBlockService.GetTitleblockId(doc, "_SCHEMATIC PLAN TITLEBLOCK");
            if (titleblockId == ElementId.InvalidElementId)
            {
                LoggingService.LogError("Could not find the '_SCHEMATIC PLAN TITLEBLOCK' titleblock");
                return 0;
            }

            // Sort levels by elevation (assuming level names contain numbers)
            var sortedLevels = LevelUtility.SortLevelsByNumber(viewsByLevel.Keys.ToList());

            // Group levels into sets of 2-4 for combined sheets
            List<List<string>> levelGroups = LevelUtility.GroupLevelsForCombinedSheets(sortedLevels);

            using (Transaction tx = new Transaction(doc, "Create Combined Sheets"))
            {
                tx.Start();

                for (int i = 0; i < levelGroups.Count; i++)
                {
                    List<string> levelGroup = levelGroups[i];

                    // Determine if we need a 2-panel or 4-panel sheet
                    bool useTwoPanel = levelGroup.Count == 2;

                    // Create a sheet
                    ViewSheet sheet = ViewSheet.Create(doc, titleblockId);
                    sheet.Name = $"Combined - Sheet {i + 1}";

                    // Place views on the sheet
                    if (useTwoPanel)
                    {
                        PlaceViewsOnTwoPanelSheet(doc, sheet, levelGroup, viewsByLevel);
                    }
                    else
                    {
                        PlaceViewsOnFourPanelSheet(doc, sheet, levelGroup, viewsByLevel);
                    }

                    LoggingService.Log($"Created combined sheet with {levelGroup.Count} levels");
                    sheetCount++;
                }

                tx.Commit();
            }

            return sheetCount;
        }

        /// <summary>
        /// Places views on a 2-panel sheet
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="sheet">The sheet</param>
        /// <param name="levelGroup">The group of levels for this sheet</param>
        /// <param name="viewsByLevel">The views grouped by level</param>
        private static void PlaceViewsOnTwoPanelSheet(Document doc, ViewSheet sheet, List<string> levelGroup, Dictionary<string, List<IPXView>> viewsByLevel)
        {
            // Panel A (top-left)
            if (levelGroup.Count > 0)
            {
                string levelName = levelGroup[0];
                IPXView view = ViewService.FindBestFittingView(viewsByLevel[levelName], TWO_PANEL_VIEW_WIDTH, TWO_PANEL_VIEW_HEIGHT);

                if (view != null)
                {
                    // Get the ElementId of the view
                    ElementId viewId = RevitViewConverter.GetElementId(view);

                    if (viewId != ElementId.InvalidElementId)
                    {
                        // Calculate the center of Panel A
                        double x = PANEL_OFFSET + (TWO_PANEL_VIEW_WIDTH / 2);
                        double y = SHEET_HEIGHT - (PANEL_OFFSET + (TWO_PANEL_VIEW_HEIGHT / 2));
                        XYZ viewCenter = new XYZ(x, y, 0);

                        PlaceViewOnSheet(doc, sheet, view, viewCenter);
                        LoggingService.Log($"Placed view {view.Name} in Panel A of sheet {sheet.Name}");
                    }
                }
            }

            // Panel B (top-right)
            if (levelGroup.Count > 1)
            {
                string levelName = levelGroup[1];
                IPXView view = ViewService.FindBestFittingView(viewsByLevel[levelName], TWO_PANEL_VIEW_WIDTH, TWO_PANEL_VIEW_HEIGHT);

                if (view != null)
                {
                    // Get the ElementId of the view
                    ElementId viewId = RevitViewConverter.GetElementId(view);

                    if (viewId != ElementId.InvalidElementId)
                    {
                        // Calculate the center of Panel B
                        double x = SHEET_WIDTH - (PANEL_OFFSET + (TWO_PANEL_VIEW_WIDTH / 2));
                        double y = SHEET_HEIGHT - (PANEL_OFFSET + (TWO_PANEL_VIEW_HEIGHT / 2));
                        XYZ viewCenter = new XYZ(x, y, 0);

                        PlaceViewOnSheet(doc, sheet, view, viewCenter);
                        LoggingService.Log($"Placed view {view.Name} in Panel B of sheet {sheet.Name}");
                    }
                }
            }
        }

        /// <summary>
        /// Places views on a 4-panel sheet
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="sheet">The sheet</param>
        /// <param name="levelGroup">The group of levels for this sheet</param>
        /// <param name="viewsByLevel">The views grouped by level</param>
        private static void PlaceViewsOnFourPanelSheet(Document doc, ViewSheet sheet, List<string> levelGroup, Dictionary<string, List<IPXView>> viewsByLevel)
        {
            // Panel A (top-left)
            if (levelGroup.Count > 0)
            {
                string levelName = levelGroup[0];
                IPXView view = ViewService.FindBestFittingView(viewsByLevel[levelName], FOUR_PANEL_VIEW_WIDTH, FOUR_PANEL_VIEW_HEIGHT);

                if (view != null)
                {
                    // Get the ElementId of the view
                    ElementId viewId = RevitViewConverter.GetElementId(view);

                    if (viewId != ElementId.InvalidElementId)
                    {
                        // Calculate the center of Panel A
                        double x = PANEL_OFFSET + (FOUR_PANEL_VIEW_WIDTH / 2);
                        double y = SHEET_HEIGHT - (PANEL_OFFSET + (FOUR_PANEL_VIEW_HEIGHT / 2));
                        XYZ viewCenter = new XYZ(x, y, 0);

                        PlaceViewOnSheet(doc, sheet, view, viewCenter);
                        LoggingService.Log($"Placed view {view.Name} in Panel A of sheet {sheet.Name}");
                    }
                }
            }

            // Panel B (top-right)
            if (levelGroup.Count > 1)
            {
                string levelName = levelGroup[1];
                IPXView view = ViewService.FindBestFittingView(viewsByLevel[levelName], FOUR_PANEL_VIEW_WIDTH, FOUR_PANEL_VIEW_HEIGHT);

                if (view != null)
                {
                    // Get the ElementId of the view
                    ElementId viewId = RevitViewConverter.GetElementId(view);

                    if (viewId != ElementId.InvalidElementId)
                    {
                        // Calculate the center of Panel B
                        double x = SHEET_WIDTH - (PANEL_OFFSET + (FOUR_PANEL_VIEW_WIDTH / 2));
                        double y = SHEET_HEIGHT - (PANEL_OFFSET + (FOUR_PANEL_VIEW_HEIGHT / 2));
                        XYZ viewCenter = new XYZ(x, y, 0);

                        PlaceViewOnSheet(doc, sheet, view, viewCenter);
                        LoggingService.Log($"Placed view {view.Name} in Panel B of sheet {sheet.Name}");
                    }
                }
            }

            // Panel C (bottom-right)
            if (levelGroup.Count > 2)
            {
                string levelName = levelGroup[2];
                IPXView view = ViewService.FindBestFittingView(viewsByLevel[levelName], FOUR_PANEL_VIEW_WIDTH, FOUR_PANEL_VIEW_HEIGHT);

                if (view != null)
                {
                    // Get the ElementId of the view
                    ElementId viewId = RevitViewConverter.GetElementId(view);

                    if (viewId != ElementId.InvalidElementId)
                    {
                        // Calculate the center of Panel C
                        double x = SHEET_WIDTH - (PANEL_OFFSET + (FOUR_PANEL_VIEW_WIDTH / 2));
                        double y = PANEL_OFFSET + (FOUR_PANEL_VIEW_HEIGHT / 2);
                        XYZ viewCenter = new XYZ(x, y, 0);

                        PlaceViewOnSheet(doc, sheet, view, viewCenter);
                        LoggingService.Log($"Placed view {view.Name} in Panel C of sheet {sheet.Name}");
                    }
                }
            }

            // Panel D (bottom-left)
            if (levelGroup.Count > 3)
            {
                string levelName = levelGroup[3];
                IPXView view = ViewService.FindBestFittingView(viewsByLevel[levelName], FOUR_PANEL_VIEW_WIDTH, FOUR_PANEL_VIEW_HEIGHT);

                if (view != null)
                {
                    // Get the ElementId of the view
                    ElementId viewId = RevitViewConverter.GetElementId(view);

                    if (viewId != ElementId.InvalidElementId)
                    {
                        // Calculate the center of Panel D
                        double x = PANEL_OFFSET + (FOUR_PANEL_VIEW_WIDTH / 2);
                        double y = PANEL_OFFSET + (FOUR_PANEL_VIEW_HEIGHT / 2);
                        XYZ viewCenter = new XYZ(x, y, 0);

                        PlaceViewOnSheet(doc, sheet, view, viewCenter);
                        LoggingService.Log($"Placed view {view.Name} in Panel D of sheet {sheet.Name}");
                    }
                }
            }
        }

        /// <summary>
        /// Creates a sheet with the specified number and name
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="sheetNumber">The sheet number</param>
        /// <param name="sheetName">The sheet name</param>
        /// <returns>The created sheet</returns>
        public static ViewSheet CreateSheet(Document doc, string sheetNumber, string sheetName)
        {
            try
            {
                // Get the titleblock family
                ElementId titleblockId = RevitTitleBlockService.GetTitleblockId(doc, "_SCHEMATIC PLAN TITLEBLOCK");
                if (titleblockId == ElementId.InvalidElementId)
                {
                    LoggingService.LogError("Could not find the '_SCHEMATIC PLAN TITLEBLOCK' titleblock");
                    return null;
                }

                ViewSheet sheet = null;
                using (Transaction tx = new Transaction(doc, "Create Sheet"))
                {
                    tx.Start();
                    try
                    {
                        sheet = ViewSheet.Create(doc, titleblockId);
                        if (sheet != null)
                        {
                            sheet.SheetNumber = sheetNumber;
                            sheet.Name = sheetName;
                        }
                        tx.Commit();
                    }
                    catch (Exception ex)
                    {
                        tx.RollBack();
                        LoggingService.LogError($"Error creating sheet: {ex.Message}");
                        return null;
                    }
                }

                return sheet;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error in CreateSheet: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Places a view on a sheet
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="sheet">The sheet</param>
        /// <param name="ipxView">The view to place</param>
        /// <param name="position">The position to place the view</param>
        /// <returns>The created viewport</returns>
        public static Viewport PlaceViewOnSheet(Document doc, ViewSheet sheet, IPXView ipxView, XYZ position)
        {
            LoggingService.Log($"Placing view {ipxView.Name} on sheet {sheet.Name}");

            // Get the Revit view from the IPXView
            ElementId viewId = RevitViewConverter.GetElementId(ipxView);

            // Check if the view can be placed on a sheet
            if (!ipxView.CanBePlacedOnSheet)
            {
                LoggingService.LogError($"View {ipxView.Name} cannot be placed on a sheet");
                return null;
            }

            if (viewId == ElementId.InvalidElementId)
            {
                LoggingService.LogError($"Could not get valid ElementId for view {ipxView.Name}");
                return null;
            }

            View view = doc.GetElement(viewId) as View;
            if (view == null)
            {
                LoggingService.LogError($"Could not get Revit view for {ipxView.Name}");
                return null;
            }


            // Create the viewport within a transaction
            Viewport viewport = null;
            using (Transaction tx = new Transaction(doc, "Place View on Sheet"))
            {
                tx.Start();
                try
                {
                    // Create the viewport
                    viewport = Viewport.Create(doc, sheet.Id, view.Id, position);

                    if (viewport != null)
                    {
                        // Set the viewport scale if available
                        Parameter scaleParam = viewport.get_Parameter(BuiltInParameter.VIEWPORT_SCALE);
                        if (scaleParam != null && !scaleParam.IsReadOnly && ipxView.Scale > 0)
                        {
                            scaleParam.Set(ipxView.Scale);
                        }
                    }

                    tx.Commit();
                }
                catch (Exception ex)
                {
                    tx.RollBack();
                    LoggingService.LogError($"Error creating viewport: {ex.Message}");
                    return null;
                }
            }

            if (viewport != null)
            {
                LoggingService.Log("View placed successfully on sheet");
            }
            else
            {
                LoggingService.LogError("Failed to create viewport");
            }

            return viewport;
        }

        /// <summary>
        /// Gets all sheets in the document
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <returns>A list of all sheets</returns>
        public static List<ViewSheet> GetAllSheets(Document doc)
        {
            try
            {
                return new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .Where(sheet => sheet != null && sheet.IsValidObject)
                    .ToList();
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error getting all sheets: {ex.Message}");
                return new List<ViewSheet>();
            }
        }

        /// <summary>
        /// Gets a sheet by its number
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="sheetNumber">The sheet number</param>
        /// <returns>The sheet with the specified number</returns>
        public static ViewSheet GetSheetByNumber(Document doc, string sheetNumber)
        {
            try
            {
                return new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .FirstOrDefault(sheet =>
                        sheet != null &&
                        sheet.IsValidObject &&
                        sheet.SheetNumber == sheetNumber);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error getting sheet by number: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets the outline of a titleblock
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="titleblockId">The titleblock ID</param>
        /// <returns>The titleblock outline</returns>
        private static Outline GetTitleblockOutline(Document doc, ElementId titleblockId)
        {
            try
            {
                Element titleblock = doc.GetElement(titleblockId);
                if (titleblock == null || !titleblock.IsValidObject)
                    return null;

                BoundingBoxXYZ bbox = titleblock.get_BoundingBox(null);
                if (bbox == null)
                    return null;

                return new Outline(bbox.Min, bbox.Max);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error getting titleblock outline: {ex.Message}");
                return null;
            }
        }
    }
}