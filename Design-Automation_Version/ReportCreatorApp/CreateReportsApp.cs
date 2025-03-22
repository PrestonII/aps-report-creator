using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using DesignAutomationFramework;
using Newtonsoft.Json;

namespace ipx.revit.reports
{
   [Autodesk.Revit.Attributes.Regeneration(Autodesk.Revit.Attributes.RegenerationOption.Manual)]
   [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
   public class CreateReportsApp : IExternalDBApplication
   {
      public ExternalDBApplicationResult OnStartup(ControlledApplication app)
      {
         DesignAutomationBridge.DesignAutomationReadyEvent += HandleDesignAutomationReadyEvent;
         return ExternalDBApplicationResult.Succeeded;
      }

      public ExternalDBApplicationResult OnShutdown(ControlledApplication app)
      {
         return ExternalDBApplicationResult.Succeeded;
      }

      public void HandleDesignAutomationReadyEvent(object sender, DesignAutomationReadyEventArgs e)
      {
         e.Succeeded = true;
         ExportToPdfs(e.DesignAutomationData);
      }

    public bool ExportToPdfs(DesignAutomationData data)
    {
        if (data == null)
        {
            Console.WriteLine("[ERROR] DesignAutomationData is null.");
            throw new ArgumentNullException(nameof(data));
        }

        Console.WriteLine("[INFO] Starting ExportToPdfs...");

        Application rvtApp = data.RevitApp;
        if (rvtApp == null)
        {
            Console.WriteLine("[ERROR] RevitApp is null.");
            throw new InvalidDataException(nameof(rvtApp));
        }

        string modelPath = data.FilePath;
        Console.WriteLine($"[DEBUG] modelPath from DesignAutomationData.FilePath: '{modelPath}'");

        if (String.IsNullOrWhiteSpace(modelPath))
        {
            Console.WriteLine("[ERROR] modelPath is null or whitespace.");
            throw new InvalidDataException(nameof(modelPath));
        }

        Document doc = data.RevitDoc;
        if (doc == null)
        {
            Console.WriteLine("[ERROR] RevitDoc is null. Could not open document.");
            throw new InvalidOperationException("Could not open document.");
        }

        Console.WriteLine("[INFO] Revit document opened successfully.");

        InputParams inputParams = InputParams.Parse("params.json");

        if (inputParams == null)
        {
            Console.WriteLine("[WARN] InputParams could not be parsed. Using defaults.");
            inputParams = new InputParams(); // fallback to all true
        }

        Console.WriteLine("[INFO] Parsed InputParams:");
        Console.WriteLine($" - DrawingSheet: {inputParams.DrawingSheet}");
        Console.WriteLine($" - ThreeD: {inputParams.ThreeD}");
        Console.WriteLine($" - Detail: {inputParams.Detail}");
        Console.WriteLine($" - Elevation: {inputParams.Elevation}");
        Console.WriteLine($" - FloorPlan: {inputParams.FloorPlan}");
        Console.WriteLine($" - Section: {inputParams.Section}");
        Console.WriteLine($" - Rendering: {inputParams.Rendering}");

        return ExportToPdfsImp(rvtApp, doc, inputParams);
    }



    private bool ExportToPdfsImp(Application rvtApp, Document doc, InputParams inputParams)
    {

      using (Transaction tx = new Transaction(doc))
      {
        tx.Start("Export PDF");

        List<View> views = new FilteredElementCollector(doc)
            .OfClass(typeof(View))
            .Cast<View>()
            .Where(vw => (
            !vw.IsTemplate && vw.CanBePrinted
            && (inputParams.DrawingSheet && vw.ViewType == ViewType.DrawingSheet
            || inputParams.ThreeD && vw.ViewType == ViewType.ThreeD
            || inputParams.Detail && vw.ViewType == ViewType.Detail
            || inputParams.Elevation && vw.ViewType == ViewType.Elevation
            || inputParams.FloorPlan && vw.ViewType == ViewType.FloorPlan
            || inputParams.Rendering && vw.ViewType == ViewType.Rendering
            || inputParams.Section && vw.ViewType == ViewType.Section)
            )
        ).ToList();

        Console.WriteLine("the number of views: " + views.Count);

        // Note: Setting the maximum number of views to be exported as 5 for demonstration purpose.
        // Remove or edit here in your production application
        const int Max_views = 5;
        IList<ElementId> viewIds = new List<ElementId>();
        for (int i = 0; i < views.Count && i < Max_views; ++i)  // To Do: edit or remove max_views as required.
        {
          Console.WriteLine(views[i].Name + @", view type is: " + views[i].ViewType.ToString());
          viewIds.Add(views[i].Id);
        }


        if (0 < views.Count)
        {
          PDFExportOptions options = new PDFExportOptions();
          options.FileName = "result"; 
          options.Combine = true;
          string workingFolder = Directory.GetCurrentDirectory();
          doc.Export(workingFolder, viewIds, options);
        }
        tx.RollBack();
      }
      return true;
    }

  }

  /// <summary>
  /// InputParams is used to parse the input Json parameters
  /// </summary>
  internal class InputParams
  {
    public bool DrawingSheet { get; set; } = true;
    public bool ThreeD { get; set; } = true;
    public bool Detail { get; set; } = true;
    public bool Elevation { get; set; } = true;
    public bool FloorPlan { get; set; } = true;
    public bool Section { get; set; } = true;
    public bool Rendering { get; set; } = true;

    static public InputParams Parse(string jsonPath)
    {
      try
      {
        if (!File.Exists(jsonPath))
          return new InputParams { DrawingSheet = true, ThreeD = true, Detail = true, Elevation = true, FloorPlan = true, Section = true, Rendering = true };

        string jsonContents = File.ReadAllText(jsonPath);
        return JsonConvert.DeserializeObject<InputParams>(jsonContents);
      }
      catch (System.Exception ex)
      {
        Console.WriteLine("Exception when parsing json file: " + ex);
        return null;
      }
    }
  }
}
