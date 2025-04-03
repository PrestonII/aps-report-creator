using System;
using System.IO;
using Autodesk.Revit.DB;
using ipx.revit.reports.Models;

namespace ipx.revit.reports.Services
{
    /// <summary>
    /// Service for handling image operations in Revit
    /// </summary>
    public class RevitImageService
    {
        private readonly Document _doc;

        /// <summary>
        /// Initializes a new instance of the RevitImageService class
        /// </summary>
        /// <param name="doc">The Revit document</param>
        public RevitImageService(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        /// <summary>
        /// Creates a new drafting view in Revit
        /// </summary>
        /// <param name="viewName">Name for the new drafting view</param>
        /// <returns>The created drafting view</returns>
        public ViewDrafting CreateDraftingView(string viewName)
        {
            Console.WriteLine($"[INFO] Creating drafting view: {viewName}");
            
            // Find the drafting view type
            ViewFamilyType draftingViewType = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(v => v.ViewFamily == ViewFamily.Drafting);
                
            if (draftingViewType == null)
            {
                throw new InvalidOperationException("Could not find drafting view type");
            }
            
            // Create the drafting view
            ViewDrafting draftingView = ViewDrafting.Create(_doc, draftingViewType.Id);
            
            // Set the name of the view
            draftingView.Name = viewName;
            
            Console.WriteLine($"[INFO] Drafting view created successfully: {draftingView.Name}");
            return draftingView;
        }

        /// <summary>
        /// Imports an image into Revit
        /// </summary>
        /// <param name="imagePath">Path to the image file</param>
        /// <returns>The imported image type</returns>
        public ImageType ImportImage(string imagePath)
        {
            Console.WriteLine($"[INFO] Importing image: {imagePath}");
            
            if (!File.Exists(imagePath))
            {
                throw new FileNotFoundException($"Image file not found: {imagePath}");
            }
            
            // Create image import options
            ImageTypeOptions options = new ImageTypeOptions(imagePath);
            options.Resolution = 300; // Set resolution to 300 DPI
            
            // Import the image
            ImageType imageType = ImageType.Create(_doc, options);
            
            Console.WriteLine($"[INFO] Image imported successfully: {Path.GetFileName(imagePath)}");
            return imageType;
        }

        /// <summary>
        /// Places an image on a drafting view
        /// </summary>
        /// <param name="view">The drafting view</param>
        /// <param name="imageType">The image type to place</param>
        /// <param name="position">The position to place the image</param>
        /// <param name="scale">The scale factor for the image</param>
        /// <returns>The placed image element</returns>
        public Element PlaceImageOnView(ViewDrafting view, ImageType imageType, XYZ position, double scale = 1.0)
        {
            Console.WriteLine($"[INFO] Placing image on view: {view.Name}");
            
            // Create a new image instance
            Element image = _doc.Create.NewDetailComponent(position, imageType.Id, view.Id);
            
            // Scale the image if needed
            if (Math.Abs(scale - 1.0) > 0.001)
            {
                // Get the bounding box of the image
                BoundingBoxXYZ bbox = image.get_BoundingBox(view);
                XYZ center = (bbox.Max + bbox.Min) / 2.0;
                
                // Create a transform for scaling
                Transform transform = Transform.CreateTranslation(-center)
                    .Multiply(Transform.CreateScale(scale))
                    .Multiply(Transform.CreateTranslation(center));
                
                // Apply the transform
                ElementTransformUtils.ModifyElement(_doc, image.Id, transform);
            }
            
            Console.WriteLine($"[INFO] Image placed successfully on view: {view.Name}");
            return image;
        }
    }
} 