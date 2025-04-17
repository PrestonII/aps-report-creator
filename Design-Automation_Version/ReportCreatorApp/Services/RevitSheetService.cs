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
        /// Places views on sheets according to the specified requirements
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <returns>The number of sheets created</returns>
        public static int PlaceViewsOnSheets(Document doc)
        {
            try
            {
                LoggingService.Log("Starting to place views on sheets...");

                // Get all floor plan views
                FilteredElementCollector collector = new FilteredElementCollector(doc);
                IList<Element> views = collector.OfClass(typeof(ViewPlan)).ToElements();
                
                // Filter to only include floor plan views
                List<ViewPlan> floorPlanViews = views
                    .Cast<ViewPlan>()
                    .Where(v => v.ViewType == ViewType.FloorPlan)
                    .ToList();

                if (floorPlanViews.Count == 0)
                {
                    LoggingService.LogError("No floor plan views found in the document");
                    return 0;
                }

                LoggingService.Log($"Found {floorPlanViews.Count} floor plan views");

                // Convert Revit views to IPXViews
                List<IPXView> ipxViews = RevitViewConverter.ConvertToIPXViews(floorPlanViews);
                
                // Group views by level
                Dictionary<string, List<IPXView>> viewsByLevel = ViewService.GroupViewsByLevel(ipxViews);
                
                // Create individual sheets
                int individualSheetCount = CreateIndividualSheets(doc, viewsByLevel);
                
                // Create combined sheets
                int combinedSheetCount = CreateCombinedSheets(doc, viewsByLevel);
                
                int totalSheetCount = individualSheetCount + combinedSheetCount;
                LoggingService.Log($"Successfully created {totalSheetCount} sheets ({individualSheetCount} individual, {combinedSheetCount} combined)");
                
                return totalSheetCount;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error placing views on sheets: {ex.Message}");
                throw ex;
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
                            Viewport.Create(doc, sheet.Id, viewId, viewCenter);
                            
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
                        
                        Viewport.Create(doc, sheet.Id, viewId, viewCenter);
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
                        
                        Viewport.Create(doc, sheet.Id, viewId, viewCenter);
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
                        
                        Viewport.Create(doc, sheet.Id, viewId, viewCenter);
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
                        
                        Viewport.Create(doc, sheet.Id, viewId, viewCenter);
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
                        
                        Viewport.Create(doc, sheet.Id, viewId, viewCenter);
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
                        
                        Viewport.Create(doc, sheet.Id, viewId, viewCenter);
                        LoggingService.Log($"Placed view {view.Name} in Panel D of sheet {sheet.Name}");
                    }
                }
            }
        }

        /// <summary>
        /// Creates a new sheet in Revit
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="sheetNumber">Sheet number</param>
        /// <param name="sheetName">Sheet name</param>
        /// <returns>The created sheet</returns>
        public static ViewSheet CreateSheet(Document doc, string sheetNumber, string sheetName)
        {
            LoggingService.Log($"Creating sheet: {sheetNumber} - {sheetName}");

            // Find a title block
            FamilySymbol titleBlock = RevitTitleBlockService.FindTitleBlock(doc);
            if (titleBlock == null)
            {
                LoggingService.LogError("Could not find a title block");
                throw new InvalidOperationException("Could not find a title block");
            }

            // Create the sheet
            ViewSheet sheet = ViewSheet.Create(doc, titleBlock.Id);
            sheet.SheetNumber = sheetNumber;
            sheet.Name = sheetName;

            LoggingService.Log($"Sheet created successfully: {sheet.SheetNumber} - {sheet.Name}");
            return sheet;
        }

        /// <summary>
        /// Places a view on a sheet
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="sheet">The sheet</param>
        /// <param name="view">The view to place</param>
        /// <param name="position">The position to place the view</param>
        /// <returns>The created viewport</returns>
        public static Viewport PlaceViewOnSheet(Document doc, ViewSheet sheet, View view, XYZ position)
        {
            LoggingService.Log($"Placing view {view.Name} on sheet {sheet.Name}");

            // Check if the view can be placed on a sheet
            if (!view.ViewType.ToString().Contains("Drafting"))
            {
                LoggingService.LogError($"View {view.Name} cannot be placed on a sheet");
                throw new InvalidOperationException($"View {view.Name} cannot be placed on a sheet");
            }

            // Create the viewport
            Viewport viewport = Viewport.Create(doc, sheet.Id, view.Id, position);

            LoggingService.Log("View placed successfully on sheet");
            return viewport;
        }

        /// <summary>
        /// Gets all sheets in the document
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <returns>List of sheets</returns>
        public static List<ViewSheet> GetAllSheets(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .WhereElementIsNotElementType()
                .Cast<ViewSheet>()
                .ToList();
        }

        /// <summary>
        /// Gets a sheet by number
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="sheetNumber">The sheet number to find</param>
        /// <returns>The sheet, or null if not found</returns>
        public static ViewSheet GetSheetByNumber(Document doc, string sheetNumber)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .WhereElementIsNotElementType()
                .Cast<ViewSheet>()
                .FirstOrDefault(s => s.SheetNumber == sheetNumber);
        }
    }
}