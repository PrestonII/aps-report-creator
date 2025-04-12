using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Autodesk.Revit.DB;

using ipx.revit.reports.Models;

namespace ipx.revit.reports.Services
{
    /// <summary>
    /// Service for generating reports with images in Revit
    /// </summary>
    public class ReportGenerationService
    {
        private readonly Document _doc;
        private readonly RevitImageService _imageService;
        private readonly RevitSheetService _sheetService;
        private readonly FileService _fileService;
        private readonly LoggingService _logger;

        /// <summary>
        /// Initializes a new instance of the ReportGenerationService class
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="username">Username for API authentication</param>
        /// <param name="password">Password for API authentication</param>
        /// <param name="environment">The environment setting for logging</param>
        public ReportGenerationService(Document doc, string username, string password, string environment = "development")
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _logger = new LoggingService(environment);
            _imageService = new RevitImageService(doc, environment);
            _sheetService = new RevitSheetService(doc, environment);
            _fileService = new FileService(username, password);
        }

        /// <summary>
        /// Generates a report with images from the provided project data
        /// </summary>
        /// <param name="projectData">The project data containing image information</param>
        /// <returns>List of sheet IDs that were created</returns>
        public List<ElementId> GenerateImageReport(ProjectData projectData)
        {
            _logger.Log("Starting image report generation process...");

            // Create a temporary folder for downloaded images
            string tempFolder = Path.Combine(Path.GetTempPath(), "RevitImages");
            Directory.CreateDirectory(tempFolder);

            // Filter for image assets only
            var imageAssets = projectData.ImageData
                .Where(a => a.AssetType.Equals("image", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!imageAssets.Any())
            {
                _logger.LogWarning("No image assets found in project data");
                return new List<ElementId>();
            }

            // Create initial sheet
            string sheetNumber = "A001";
            ViewSheet reportSheet = _sheetService.CreateSheet(sheetNumber, $"{projectData.ProjectName} - Asset Report 1");
            List<ElementId> viewsToExport = new List<ElementId> { reportSheet.Id };

            // Define layout parameters
            int imagesPerRow = 2;
            int rowsPerSheet = 2;
            double imageWidth = 8.0;  // inches
            double imageHeight = 6.0; // inches
            double imageSpacing = 1.0; // inches

            // Calculate starting position on sheet (centered)
            XYZ sheetOrigin = new XYZ(4.0, 8.0, 0); // inches from bottom-left

            int imageCount = 0;

            foreach (var asset in imageAssets)
            {
                try
                {
                    string imageUrl = asset.AssetUrlOverride ?? asset.AssetUrl;
                    if (string.IsNullOrEmpty(imageUrl))
                    {
                        _logger.LogWarning($"No URL found for asset: {asset.AssetName}. Skipping.");
                        continue;
                    }

                    string imagePath = _fileService.DownloadFile(imageUrl, Path.Combine(tempFolder, Path.GetFileName(imageUrl)));
                    _logger.Log($"Downloaded image to: {imagePath}");

                    // Create a drafting view for the image
                    string viewName = $"Image - {asset.AssetName}";
                    ViewDrafting draftingView = _imageService.CreateDraftingView(viewName);

                    // Import and place the image
                    var imageType = _imageService.ImportImage(_doc, imagePath);
                    var location = new XYZ(0, 0, 0); // Default location at origin
                    var element = _imageService.PlaceImageOnView(_doc, imageType, draftingView, location, 1.0);

                    // Calculate position on the sheet
                    int row = imageCount / imagesPerRow;
                    int col = imageCount % imagesPerRow;

                    // Check if we need a new sheet
                    if (row >= rowsPerSheet)
                    {
                        // Create a new sheet
                        string newSheetNumber = $"A{(viewsToExport.Count + 1):000}";
                        reportSheet = _sheetService.CreateSheet(newSheetNumber, $"{projectData.ProjectName} - Asset Report {viewsToExport.Count + 1}");
                        viewsToExport.Add(reportSheet.Id);

                        // Reset counters
                        row = 0;
                        imageCount = 0;
                    }

                    // Calculate position on current sheet
                    XYZ position = new XYZ(
                        sheetOrigin.X + col * (imageWidth + imageSpacing),
                        sheetOrigin.Y - row * (imageHeight + imageSpacing),
                        0
                    );

                    // Place the drafting view on the sheet
                    Viewport viewport = _sheetService.PlaceViewOnSheet(reportSheet, draftingView, position);

                    // Add a label below the image
                    TextNote.Create(_doc, reportSheet.Id,
                        new XYZ(position.X, position.Y - 0.5, 0),
                        asset.AssetName,
                        new ElementId(BuiltInParameter.TEXT_FONT));

                    imageCount++;
                    _logger.Log($"Successfully processed asset: {asset.AssetName}");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to process asset {asset.AssetName}", ex);
                    // Continue with the next asset
                }
            }

            _logger.Log($"Image report generation completed with {viewsToExport.Count} sheets");
            return viewsToExport;
        }

        /// <summary>
        /// Places images on individual sheets from Airtable data
        /// </summary>
        /// <param name="projectData">The project data containing image information</param>
        /// <returns>List of sheet IDs that were created</returns>
        public List<ElementId> PlaceImagesOnIndividualSheets(ProjectData projectData)
        {
            _logger.Log("Starting image sheet placement process...");

            // Create a temporary folder for downloaded images
            string tempFolder = Path.Combine(Path.GetTempPath(), "RevitImages");
            Directory.CreateDirectory(tempFolder);

            // Filter for image assets only
            var imageAssets = projectData.ImageData
                .Where(a => a.AssetType.Equals("image", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!imageAssets.Any())
            {
                _logger.LogWarning("No image assets found in project data");
                return new List<ElementId>();
            }

            List<ElementId> createdSheets = new List<ElementId>();

            foreach (var asset in imageAssets)
            {
                try
                {
                    _logger.Log($"Processing asset: {asset.AssetName}");

                    // 1. Download the image based on asset_url
                    string imageUrl = asset.AssetUrlOverride ?? asset.AssetUrl;
                    if (string.IsNullOrEmpty(imageUrl))
                    {
                        _logger.LogWarning($"No URL found for asset: {asset.AssetName}. Skipping.");
                        continue;
                    }

                    string imagePath = _fileService.DownloadFile(imageUrl, Path.Combine(tempFolder, Path.GetFileName(imageUrl)));
                    _logger.Log($"Downloaded image to: {imagePath}");

                    // 2. Create a new Drafting View with a name matching asset_name
                    string viewName = asset.AssetName;
                    ViewDrafting draftingView = _imageService.CreateDraftingView(viewName);

                    // Import and place the image in the drafting view
                    var imageType = _imageService.ImportImage(_doc, imagePath);
                    var location = new XYZ(0, 0, 0); // Default location at origin
                    var element = _imageService.PlaceImageOnView(_doc, imageType, draftingView, location, 1.0);

                    // 3. Create a new sheet for this image
                    string sheetNumber = $"I{(createdSheets.Count + 1):000}"; // I for Image sheets
                    string sheetName = $"{asset.AssetName} Sheet";
                    ViewSheet sheet = _sheetService.CreateSheet(sheetNumber, sheetName);

                    // Get the sheet size
                    double sheetWidth = 0;
                    double sheetHeight = 0;

                    // Find the title block outline
                    var titleBlock = new FilteredElementCollector(_doc, sheet.Id)
                        .OfCategory(BuiltInCategory.OST_TitleBlocks)
                        .WhereElementIsNotElementType()
                        .FirstElement();

                    if (titleBlock != null)
                    {
                        var bbox = titleBlock.get_BoundingBox(sheet);
                        if (bbox != null)
                        {
                            sheetWidth = bbox.Max.X - bbox.Min.X;
                            sheetHeight = bbox.Max.Y - bbox.Min.Y;
                        }
                    }

                    if (sheetWidth <= 0 || sheetHeight <= 0)
                    {
                        // Default to ANSI B size (11" x 17") if we can't determine actual size
                        sheetWidth = 17.0;
                        sheetHeight = 11.0;
                    }

                    // Calculate position 1" from top and right sides of the sheet
                    // In Revit, Y increases going up, so for "top" we use (sheetHeight - 1)
                    XYZ position = new XYZ(sheetWidth - 1.0, sheetHeight - 1.0, 0);

                    // Place the drafting view on the sheet
                    _logger.Log($"Placing view on sheet at position: ({position.X}, {position.Y})");
                    Viewport viewport = _sheetService.PlaceViewOnSheet(sheet, draftingView, position);

                    createdSheets.Add(sheet.Id);
                    _logger.Log($"Successfully created sheet for asset: {asset.AssetName}");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to process asset {asset.AssetName}", ex);
                    // Continue with the next asset
                }
            }

            _logger.Log($"Image sheet placement completed with {createdSheets.Count} sheets");
            return createdSheets;
        }

        /// <summary>
        /// Exports sheets to PDF
        /// </summary>
        /// <param name="sheetIds">List of sheet IDs to export</param>
        /// <param name="outputFileName">Name of the output PDF file</param>
        /// <returns>True if export was successful</returns>
        public bool ExportSheetsToPdf(List<ElementId> sheetIds, string outputFileName)
        {
            if (sheetIds == null || sheetIds.Count == 0)
            {
                _logger.LogWarning("No sheets to export");
                return false;
            }

            try
            {
                PDFExportOptions options = new PDFExportOptions();
                options.FileName = outputFileName ?? "AssetReport";
                options.Combine = true;

                string workingFolder = Directory.GetCurrentDirectory();
                _logger.Log($"Exporting {sheetIds.Count} sheets to PDF in folder: {workingFolder}");

                _doc.Export(workingFolder, sheetIds, options);
                _logger.Log($"PDF export completed successfully: {options.FileName}.pdf");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to export PDF", ex);
                return false;
            }
        }
    }
}