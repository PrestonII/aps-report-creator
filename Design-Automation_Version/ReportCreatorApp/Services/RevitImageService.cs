using System;
using System.Linq;

using Autodesk.Revit.DB;

namespace ipx.revit.reports.Services
{
    /// <summary>
    /// Service for handling image operations in Revit
    /// </summary>
    public static class RevitImageService
    {
        /// <summary>
        /// Creates a new drafting view in Revit
        /// </summary>
        /// <param name="viewName">Name for the new drafting view</param>
        /// <returns>The created drafting view</returns>
        public static ViewDrafting CreateDraftingView(Document _doc, string viewName)
        {
            try
            {
                LoggingService.Log($"Creating drafting view: {viewName}");

                // Find the drafting view type
                ViewFamilyType? draftingViewType = new FilteredElementCollector(_doc)
                    .OfClass(typeof(ViewFamilyType))
                    .FirstOrDefault(v => v is ViewFamilyType vft && vft.ViewFamily == ViewFamily.Drafting) as ViewFamilyType;

                if (draftingViewType == null)
                {
                    LoggingService.LogError("Could not find drafting view type");
                    throw new InvalidOperationException("Could not find drafting view type");
                }


                // Create the drafting view
                ViewDrafting draftingView = ViewDrafting.Create(_doc, draftingViewType.Id);


                // Set the name of the view
                draftingView.Name = viewName;


                LoggingService.Log($"Drafting view created successfully: {draftingView.Name}");
                return draftingView;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Failed to creating drafting view: {ex.Message}");
                throw ex;
            }
        }

        /// <summary>
        /// Imports an image into Revit
        /// </summary>
        /// <param name="imagePath">Path to the image file</param>
        /// <returns>The imported image type</returns>
        public static ImageType ImportImage(Document doc, string imagePath)
        {
            LoggingService.LogDebug($"Importing image from path: {imagePath}");

            var imageRef = ExternalResourceReference.CreateLocalResource(
                doc,
                ExternalResourceTypes.BuiltInExternalResourceTypes.Image,
                ModelPathUtils.ConvertUserVisiblePathToModelPath(imagePath),
                PathType.Relative
            );

            var options = new ImageTypeOptions(imageRef, ImageTypeSource.Import)
            {
                Resolution = 300
            };

            LoggingService.Log("Image import options created successfully");
            return ImageType.Create(doc, options);
        }

        /// <summary>
        /// Places an image on a drafting view
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="imageType">The image type to place</param>
        /// <param name="view">The drafting view</param>
        /// <param name="location">The position to place the image</param>
        /// <param name="scale">The scale factor for the image</param>
        /// <returns>The placed image element</returns>
        public static Element PlaceImageOnView(Document doc, ImageType imageType, View view, XYZ? location = null, double scale = 1.0)
        {
            location ??= new XYZ(0, 0, 0);
            LoggingService.LogDebug($"Placing image on view at location: ({location.X}, {location.Y}, {location.Z}) with scale: {scale}");

            // Create placement options for the image
            // BoxPlacement.Center means the center of the image will be placed at the location
            ImagePlacementOptions placementOptions = new ImagePlacementOptions(location, BoxPlacement.Center);

            // Create a new image instance at the specified location
            ImageInstance image = ImageInstance.Create(doc, view, imageType.Id, placementOptions);

            // Apply scaling if needed (using a different approach in Revit 2023)
            if (Math.Abs(scale - 1.0) > 0.001)
            {
                try
                {
                    // Get the image dimensions from the image instance
                    Parameter widthParam = image.get_Parameter(BuiltInParameter.RASTER_SHEETWIDTH);
                    Parameter heightParam = image.get_Parameter(BuiltInParameter.RASTER_SHEETHEIGHT);

                    if (widthParam != null && heightParam != null)
                    {
                        // Get current values
                        double currentWidth = widthParam.AsDouble();
                        double currentHeight = heightParam.AsDouble();

                        // Set new scaled values
                        widthParam.Set(currentWidth * scale);
                        heightParam.Set(currentHeight * scale);

                        LoggingService.LogDebug($"Applied scaling of {scale} to image dimensions");
                    }
                    else
                    {
                        LoggingService.LogWarning("Could not find width/height parameters for scaling the image");
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.LogWarning($"Error applying image scaling: {ex.Message}");
                }
            }

            LoggingService.Log("Image placed successfully on view");
            return image;
        }
    }
}