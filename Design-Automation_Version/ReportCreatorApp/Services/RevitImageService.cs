using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
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
                .FirstOrDefault(v => v is ViewFamilyType vft && vft.ViewFamily == ViewFamily.Drafting) as ViewFamilyType;
                
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
        public ImageType ImportImage(Document doc, string imagePath)
        {
            var imageRef = new ExternalResourceReference(
                new Guid("00000000-0000-0000-0000-000000000000"), // Default version
                new Dictionary<string, string>(), // No additional parameters
                "ImageType", // Resource type
                imagePath); // Resource path

            var options = new ImageTypeOptions(imageRef, ImageTypeSource.Import)
            {
                Resolution = 300
            };

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
        public Element PlaceImageOnView(Document doc, ImageType imageType, View view, XYZ location, double scale)
        {
            // Create a new image instance at the specified location
            var element = doc.Create.NewDetailCurve(view, Line.CreateBound(location, location.Add(XYZ.BasisX)));
            
            // Apply scaling transformation
            var transform = Transform.Identity;
            transform.BasisX = transform.BasisX.Multiply(scale);
            transform.BasisY = transform.BasisY.Multiply(scale);
            transform.BasisZ = transform.BasisZ.Multiply(scale);
            ElementTransformUtils.MoveElement(doc, element.Id, transform.Origin);
            
            return element;
        }
    }
} 