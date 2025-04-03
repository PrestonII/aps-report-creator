using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.ApplicationServices;
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
        
        /// <summary>
        /// Initializes a new instance of the ReportGenerationService class
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="username">Username for API authentication</param>
        /// <param name="password">Password for API authentication</param>
        public ReportGenerationService(Document doc, string username, string password)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _imageService = new RevitImageService(doc);
            _sheetService = new RevitSheetService(doc);
            _fileService = new FileService(username, password);
        }
        
        /// <summary>
        /// Generates a report with images from the provided project data
        /// </summary>
        /// <param name="projectData">The project data containing image information</param>
        /// <returns>List of sheet IDs that were created</returns>
        public List<ElementId> GenerateImageReport(ProjectData projectData)
        {
            Console.WriteLine("[INFO] Starting image report generation process...");
            
            // Create a temporary folder for downloaded images
            string tempFolder = Path.Combine(Path.GetTempPath(), "RevitImages");
            Directory.CreateDirectory(tempFolder);
            
            // Filter for image assets only
            var imageAssets = projectData.ImageData
                .Where(a => a.AssetType.Equals("image", StringComparison.OrdinalIgnoreCase))
                .ToList();
                
            Console.WriteLine($"[INFO] Found {imageAssets.Count} image assets to process");
            
            // Create a report sheet
            ViewSheet reportSheet = _sheetService.CreateSheet("A001", $"{projectData.ProjectName} - Asset Report");
            Console.WriteLine($"[INFO] Created report sheet: {reportSheet.Name}");
            
            // Track the views we'll export to PDF
            List<ElementId> viewsToExport = new List<ElementId> { reportSheet.Id };
            
            // Process each image asset
            int imageCount = 0;
            int rowsPerSheet = 2;
            int imagesPerRow = 2;
            double imageSpacing = 1.0; // 1 foot spacing
            double imageWidth = 3.0;   // 3 feet width
            double imageHeight = 2.0;  // 2 feet height
            
            // Calculate starting position (top-left of sheet with some margin)
            XYZ sheetOrigin = new XYZ(1.0, 10.0, 0.0);
            
            foreach (var asset in imageAssets)
            {
                try
                {
                    Console.WriteLine($"[INFO] Processing asset: {asset.AssetName}");
                    
                    // Download the image
                    string imageUrl = asset.AssetUrlOverride ?? asset.AssetUrl;
                    if (string.IsNullOrEmpty(imageUrl))
                    {
                        Console.WriteLine($"[WARNING] No URL found for asset: {asset.AssetName}. Skipping.");
                        continue;
                    }
                    
                    string imagePath = _fileService.DownloadImage(imageUrl, tempFolder);
                    Console.WriteLine($"[INFO] Downloaded image to: {imagePath}");
                    
                    // Create a drafting view for the image
                    string viewName = $"Image - {asset.AssetName}";
                    ViewDrafting draftingView = _imageService.CreateDraftingView(viewName);
                    
                    // Import the image into Revit
                    ImageType imageType = _imageService.ImportImage(imagePath);
                    
                    // Place the image on the drafting view
                    Element placedImage = _imageService.PlaceImageOnView(draftingView, imageType, new XYZ(0, 0, 0));
                    
                    // Calculate position on the sheet
                    int row = imageCount / imagesPerRow;
                    int col = imageCount % imagesPerRow;
                    
                    // Check if we need a new sheet
                    if (row >= rowsPerSheet)
                    {
                        // Create a new sheet
                        string sheetNumber = $"A{(viewsToExport.Count + 1):000}";
                        reportSheet = _sheetService.CreateSheet(sheetNumber, $"{projectData.ProjectName} - Asset Report {viewsToExport.Count + 1}");
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
                        new ElementId(BuiltInParameter.TEXT_FONT_REGULAR));
                    
                    imageCount++;
                    Console.WriteLine($"[INFO] Successfully processed asset: {asset.AssetName}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Failed to process asset {asset.AssetName}: {ex.Message}");
                    // Continue with the next asset
                }
            }
            
            Console.WriteLine($"[INFO] Image report generation completed with {viewsToExport.Count} sheets");
            return viewsToExport;
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
                Console.WriteLine("[WARNING] No sheets to export");
                return false;
            }
            
            try
            {
                PDFExportOptions options = new PDFExportOptions();
                options.FileName = outputFileName ?? "AssetReport";
                options.Combine = true;
                
                string workingFolder = Directory.GetCurrentDirectory();
                Console.WriteLine($"[INFO] Exporting {sheetIds.Count} sheets to PDF in folder: {workingFolder}");
                
                _doc.Export(workingFolder, sheetIds, options);
                Console.WriteLine($"[INFO] PDF export completed successfully: {options.FileName}.pdf");
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to export PDF: {ex.Message}");
                return false;
            }
        }
    }
} 