using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.DB;

using ipx.revit.reports._Constants;
using ipx.revit.reports.Models;

namespace ipx.revit.reports.Services
{
    /// <summary>
    /// Service for handling sheet operations in Revit
    /// </summary>
    public static class RevitSheetService
    {
        // Sheet dimensions (in feet)
        private const double SHEET_WIDTH = 8.5;  // Letter size width
        private const double SHEET_HEIGHT = 11.0; // Letter size height

        // Margin constants (in feet)
        private const double SHEET_MARGIN = 0.25 / 12.0;  // 0.25 inches in feet

        // Individual sheet view constraints
        private const double INDIVIDUAL_VIEW_MAX_WIDTH = 10.5 / 12.0;  // 10.5 inches in feet
        private const double INDIVIDUAL_VIEW_MAX_HEIGHT = 7.25 / 12.0; // 7.25 inches in feet

        // Combined sheet panel dimensions
        private const double PANEL_OFFSET = 0.25 / 12.0;  // 0.25 inches in feet
        private const double TWO_PANEL_VIEW_WIDTH = 5.125 / 12.0;  // 5.125 inches in feet
        private const double TWO_PANEL_VIEW_HEIGHT = 7.25 / 12.0;  // 7.25 inches in feet
        private const double FOUR_PANEL_VIEW_WIDTH = 5.125 / 12.0;  // 5.125 inches in feet
        private const double FOUR_PANEL_VIEW_HEIGHT = 3.5 / 12.0;   // 3.5 inches in feet

        /// <summary>
        /// Creates sheets for views and places views on sheets
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="views">The views to create sheets for</param>
        /// <returns>The number of sheets created and views placed</returns>
        public static int CreateAndPlaceViewsOnSheets(Document doc, IList<ViewPlan> views)
        {
            try
            {
                LoggingService.Log("Starting to create sheets and place views...");

                // Convert ViewPlans to IPXViews
                var ipxViews = RevitViewConverter.ConvertToIPXViews(views);
                if (ipxViews.Count == 0)
                {
                    LoggingService.LogWarning("No valid views to create sheets for");
                    return 0;
                }

                // Group views by level
                var viewsByLevel = ViewService.GroupViewsByLevel(ipxViews);
                LoggingService.Log($"Found {viewsByLevel.Count} unique levels from views");

                // Create individual sheets
                int individualSheetCount = CreateIndividualSheets(doc, viewsByLevel);
                LoggingService.Log($"Created {individualSheetCount} individual sheets");

                // Create combined sheets
                int combinedSheetCount = CreateCombinedSheets(doc, viewsByLevel);
                LoggingService.Log($"Created {combinedSheetCount} combined sheets");

                return individualSheetCount + combinedSheetCount;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error creating and placing views on sheets: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Creates individual sheets for each level
        /// </summary>
        private static int CreateIndividualSheets(Document doc, Dictionary<string, List<IPXView>> viewsByLevel)
        {
            int sheetCount = 0;

            // Get the titleblock family
            ElementId titleblockId = RevitTitleBlockService.GetTitleblockId(doc, CONSTANTS._TITLEBLOCKNAME);
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

                    // Find the best fitting view for individual sheet
                    IPXView bestView = ViewService.FindBestFittingView(views, INDIVIDUAL_VIEW_MAX_WIDTH, INDIVIDUAL_VIEW_MAX_HEIGHT);

                    if (bestView != null)
                    {
                        // Create a sheet
                        ViewSheet sheet = ViewSheet.Create(doc, titleblockId);
                        sheet.Name = $"Individual - {levelName}";

                        // Place the view on the sheet relative to the titleblock's position
                        // The view should be centered on the sheet, so we'll use the sheet dimensions
                        // but offset from the titleblock's position
                        XYZ viewCenter = new XYZ(0, 0, 0);  // Position relative to titleblock
                        PlaceViewOnSheet(doc, sheet, bestView, viewCenter);

                        LoggingService.Log($"Created individual sheet for level {levelName} with view {bestView.Name}");
                        sheetCount++;
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
        private static int CreateCombinedSheets(Document doc, Dictionary<string, List<IPXView>> viewsByLevel)
        {
            int sheetCount = 0;

            // Get the titleblock family
            ElementId titleblockId = RevitTitleBlockService.GetTitleblockId(doc, CONSTANTS._TITLEBLOCKNAME);
            if (titleblockId == ElementId.InvalidElementId)
            {
                LoggingService.LogError("Could not find the '_SCHEMATIC PLAN TITLEBLOCK' titleblock");
                return 0;
            }

            // Sort levels by name to ensure consistent ordering
            var sortedLevels = viewsByLevel.Keys.OrderBy(l => l).ToList();

            // Group levels into sets of 2-4 for combined sheets
            var levelGroups = new List<List<string>>();
            var currentGroup = new List<string>();

            foreach (var level in sortedLevels)
            {
                currentGroup.Add(level);
                if (currentGroup.Count == 4)
                {
                    levelGroups.Add(currentGroup);
                    currentGroup = new List<string>();
                }
            }

            if (currentGroup.Count > 0)
            {
                levelGroups.Add(currentGroup);
            }

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
        private static void PlaceViewsOnTwoPanelSheet(Document doc, ViewSheet sheet, List<string> levelGroup, Dictionary<string, List<IPXView>> viewsByLevel)
        {
            // Panel A (top-left)
            if (levelGroup.Count > 0)
            {
                string levelName = levelGroup[0];
                IPXView view = ViewService.FindBestFittingView(viewsByLevel[levelName], TWO_PANEL_VIEW_WIDTH, TWO_PANEL_VIEW_HEIGHT);

                if (view != null)
                {
                    // Calculate the center of Panel A
                    double x = PANEL_OFFSET + (TWO_PANEL_VIEW_WIDTH / 2);
                    double y = SHEET_HEIGHT - (PANEL_OFFSET + (TWO_PANEL_VIEW_HEIGHT / 2));
                    XYZ viewCenter = new XYZ(x, y, 0);

                    PlaceViewOnSheet(doc, sheet, view, viewCenter);
                    LoggingService.Log($"Placed view {view.Name} in Panel A of sheet {sheet.Name}");
                }
            }

            // Panel B (top-right)
            if (levelGroup.Count > 1)
            {
                string levelName = levelGroup[1];
                IPXView view = ViewService.FindBestFittingView(viewsByLevel[levelName], TWO_PANEL_VIEW_WIDTH, TWO_PANEL_VIEW_HEIGHT);

                if (view != null)
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

        /// <summary>
        /// Places views on a 4-panel sheet
        /// </summary>
        private static void PlaceViewsOnFourPanelSheet(Document doc, ViewSheet sheet, List<string> levelGroup, Dictionary<string, List<IPXView>> viewsByLevel)
        {
            // Panel A (top-left)
            if (levelGroup.Count > 0)
            {
                string levelName = levelGroup[0];
                IPXView view = ViewService.FindBestFittingView(viewsByLevel[levelName], FOUR_PANEL_VIEW_WIDTH, FOUR_PANEL_VIEW_HEIGHT);

                if (view != null)
                {
                    // Calculate the center of Panel A
                    double x = PANEL_OFFSET + (FOUR_PANEL_VIEW_WIDTH / 2);
                    double y = SHEET_HEIGHT - (PANEL_OFFSET + (FOUR_PANEL_VIEW_HEIGHT / 2));
                    XYZ viewCenter = new XYZ(x, y, 0);

                    PlaceViewOnSheet(doc, sheet, view, viewCenter);
                    LoggingService.Log($"Placed view {view.Name} in Panel A of sheet {sheet.Name}");
                }
            }

            // Panel B (top-right)
            if (levelGroup.Count > 1)
            {
                string levelName = levelGroup[1];
                IPXView view = ViewService.FindBestFittingView(viewsByLevel[levelName], FOUR_PANEL_VIEW_WIDTH, FOUR_PANEL_VIEW_HEIGHT);

                if (view != null)
                {
                    // Calculate the center of Panel B
                    double x = SHEET_WIDTH - (PANEL_OFFSET + (FOUR_PANEL_VIEW_WIDTH / 2));
                    double y = SHEET_HEIGHT - (PANEL_OFFSET + (FOUR_PANEL_VIEW_HEIGHT / 2));
                    XYZ viewCenter = new XYZ(x, y, 0);

                    PlaceViewOnSheet(doc, sheet, view, viewCenter);
                    LoggingService.Log($"Placed view {view.Name} in Panel B of sheet {sheet.Name}");
                }
            }

            // Panel C (bottom-right)
            if (levelGroup.Count > 2)
            {
                string levelName = levelGroup[2];
                IPXView view = ViewService.FindBestFittingView(viewsByLevel[levelName], FOUR_PANEL_VIEW_WIDTH, FOUR_PANEL_VIEW_HEIGHT);

                if (view != null)
                {
                    // Calculate the center of Panel C
                    double x = SHEET_WIDTH - (PANEL_OFFSET + (FOUR_PANEL_VIEW_WIDTH / 2));
                    double y = PANEL_OFFSET + (FOUR_PANEL_VIEW_HEIGHT / 2);
                    XYZ viewCenter = new XYZ(x, y, 0);

                    PlaceViewOnSheet(doc, sheet, view, viewCenter);
                    LoggingService.Log($"Placed view {view.Name} in Panel C of sheet {sheet.Name}");
                }
            }

            // Panel D (bottom-left)
            if (levelGroup.Count > 3)
            {
                string levelName = levelGroup[3];
                IPXView view = ViewService.FindBestFittingView(viewsByLevel[levelName], FOUR_PANEL_VIEW_WIDTH, FOUR_PANEL_VIEW_HEIGHT);

                if (view != null)
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

        /// <summary>
        /// Places a view on a sheet
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="sheet">The sheet</param>
        /// <param name="ipxView">The view to place</param>
        /// <param name="position">The position to place the view relative to the titleblock</param>
        /// <returns>The created viewport</returns>
        public static Viewport PlaceViewOnSheet(Document doc, ViewSheet sheet, IPXView ipxView, XYZ position, ElementId? titleblockFamilyId = null)
        {
            try
            {
                LoggingService.Log($"Attempting to place view {ipxView.Name} on sheet {sheet.Name}");

                if (titleblockFamilyId == null)
                {
                    // Get the titleblock family ID
                    ElementId titleblockId = RevitTitleBlockService.GetTitleblockId(doc, CONSTANTS._TITLEBLOCKNAME);
                    titleblockFamilyId = (doc.GetElement(titleblockId) as FamilySymbol).Family.Id;
                    if (titleblockFamilyId == ElementId.InvalidElementId)
                    {
                        LoggingService.LogError($"Could not find titleblock family {CONSTANTS._TITLEBLOCKNAME}");
                        return null;
                    }
                    LoggingService.Log($"Found titleblock family ID: {titleblockFamilyId}");
                }

                // Find the titleblock instance on this sheet
                FamilyInstance titleblockInstance = FindTitleblockOnSheet(doc, sheet, titleblockFamilyId);

                if (titleblockInstance == null)
                {
                    LoggingService.Log($"No titleblock found on sheet {sheet.Name}, but continuing with view placement");
                    // We'll continue without a titleblock, using the sheet's origin as reference
                }
                else
                {
                    LoggingService.Log($"Titleblock already exists on sheet {sheet.Name} with ID: {titleblockInstance.Id}");
                }

                // Get the Revit view from the IPXView
                View revitView = GetRevitView(doc, ipxView);
                if (revitView == null)
                {
                    LoggingService.LogError($"Could not get Revit view for {ipxView.Name}");
                    return null;
                }

                // Calculate the absolute position
                // If we have a titleblock, use its position as reference
                // Otherwise, use the position directly
                XYZ absolutePosition;
                if (titleblockInstance != null)
                {
                    // Get the titleblock's position
                    LocationPoint locationPoint = titleblockInstance.Location as LocationPoint;
                    if (locationPoint != null)
                    {
                        XYZ titleblockPosition = locationPoint.Point;
                        LoggingService.Log($"Titleblock position: ({titleblockPosition.X}, {titleblockPosition.Y})");

                        absolutePosition = new XYZ(
                            titleblockPosition.X + position.X,
                            titleblockPosition.Y + position.Y,
                            0
                        );
                    }
                    else
                    {
                        // Fallback to using the position directly
                        absolutePosition = position;
                    }
                }
                else
                {
                    // No titleblock, use the position directly
                    absolutePosition = position;
                }

                // Create the viewport
                return CreateViewport(doc, sheet, revitView, absolutePosition, ipxView);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error placing view on sheet: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Creates a titleblock on a sheet if it doesn't already exist
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="sheet">The sheet</param>
        /// <param name="titleblockFamilyId">The titleblock family ID</param>
        /// <returns>True if the titleblock was created or already exists, false otherwise</returns>
        public static bool EnsureTitleblockExists(Document doc, ViewSheet sheet, ElementId titleblockFamilyId)
        {
            try
            {
                // Check if the titleblock already exists
                FamilyInstance existingTitleblock = FindTitleblockOnSheet(doc, sheet, titleblockFamilyId);
                if (existingTitleblock != null)
                {
                    LoggingService.Log($"Titleblock already exists on sheet {sheet.Name} with ID: {existingTitleblock.Id}");
                    return true;
                }

                LoggingService.Log($"No titleblock found on sheet {sheet.Name}, creating one...");

                // Create the titleblock on the sheet
                FamilyInstance newTitleblockInstance = doc.Create.NewFamilyInstance(
                    new XYZ(0, 0, 0), // Position at origin
                    doc.GetElement(titleblockFamilyId) as FamilySymbol,
                    sheet,
                    Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

                if (newTitleblockInstance == null)
                {
                    LoggingService.LogError($"Failed to create titleblock on sheet {sheet.Name}");
                    return false;
                }

                LoggingService.Log($"Created titleblock on sheet {sheet.Name}");
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error ensuring titleblock exists: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Finds a titleblock on a sheet
        /// </summary>
        private static FamilyInstance FindTitleblockOnSheet(Document doc, ViewSheet sheet, ElementId titleblockFamilyId)
        {
            // Create a collector to find the titleblock on this sheet
            FilteredElementCollector collector = new FilteredElementCollector(doc, sheet.Id);
            collector.OfClass(typeof(FamilyInstance));

            int familyInstanceCount = 0;
            foreach (Element element in collector)
            {
                familyInstanceCount++;
                if (element is FamilyInstance instance)
                {
                    LoggingService.Log($"Found family instance on sheet: {instance.Symbol.Family.Name}, ID: {instance.Symbol.Family.Id}");
                    if (instance.Symbol.Family.Id == titleblockFamilyId)
                    {
                        LoggingService.Log($"Found matching titleblock instance: {instance.Id}");
                        return instance;
                    }
                }
            }

            LoggingService.Log($"Found {familyInstanceCount} family instances on sheet {sheet.Name}");
            return null;
        }

        /// <summary>
        /// Gets a Revit view from an IPXView
        /// </summary>
        private static View GetRevitView(Document doc, IPXView ipxView)
        {
            // Check if the view can be placed on a sheet
            if (!ipxView.CanBePlacedOnSheet)
            {
                LoggingService.LogError($"View {ipxView.Name} cannot be placed on a sheet");
                return null;
            }

            ElementId viewId = RevitViewConverter.GetElementId(ipxView);
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

            return view;
        }

        /// <summary>
        /// Creates a viewport on a sheet
        /// </summary>
        private static Viewport CreateViewport(Document doc, ViewSheet sheet, View view, XYZ position, IPXView ipxView)
        {
            // Create the viewport
            Viewport viewport = Viewport.Create(doc, sheet.Id, view.Id, position);
            if (viewport != null)
            {
                // Set the viewport scale if available
                Parameter scaleParam = viewport.get_Parameter(BuiltInParameter.VIEWPORT_SCALE);
                if (scaleParam != null && !scaleParam.IsReadOnly && ipxView.Scale > 0)
                {
                    scaleParam.Set(ipxView.Scale);
                }

                LoggingService.Log($"Successfully placed view {ipxView.Name} on sheet {sheet.Name} at position ({position.X}, {position.Y})");
            }
            else
            {
                LoggingService.LogError($"Failed to create viewport for view {ipxView.Name}");
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

        /// <summary>
        /// Creates sheets for each level
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="levels">The levels to create sheets for</param>
        /// <returns>The created sheets</returns>
        public static List<ViewSheet> CreateSheetsForLevels(Document doc, List<Level> levels)
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
                        ElementId titleblockId = RevitTitleBlockService.GetTitleblockId(doc, CONSTANTS._TITLEBLOCKNAME);
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
    }

    /// <summary>
    /// Comparer for Level objects
    /// </summary>
    class LevelEqualityComparer : IEqualityComparer<Level>
    {
        public bool Equals(Level? l1, Level? l2)
        {
            if (ReferenceEquals(l1, l2))
                return true;

            if (l2 is null || l1 is null)
                return false;

            return l1.Name == l2.Name && l1.UniqueId == l2.UniqueId;
        }

        public int GetHashCode(Level level) => $"{level.UniqueId}^{level.Name}".GetHashCode();
    }
}