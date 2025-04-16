using System;
using System.IO;
using System.Linq;

using Autodesk.Revit.DB;

namespace ipx.revit.reports.Services
{
    public static class RevitLinkService
    {
        /// <summary>
        /// Links all Revit files in the same directory as the host file, except for the host file itself and "input.rvt"
        /// </summary>
        /// <param name="doc">The host document</param>
        /// <param name="hostFilePath">The path to the host file</param>
        /// <returns>The number of files linked</returns>
        public static int LinkRevitFiles(Document doc, string hostFilePath)
        {
            try
            {
                LoggingService.Log("Starting to link Revit files...");

                // Get the directory of the host file
                string directory = Path.GetDirectoryName(hostFilePath);
                if (string.IsNullOrEmpty(directory))
                {
                    LoggingService.LogError("Could not determine directory from host file path");
                    return 0;
                }

                // Get all .rvt files in the directory
                string[] rvtFiles = Directory.GetFiles(directory, "*.rvt");

                // Filter out the host file and "input.rvt"
                string hostFileName = Path.GetFileName(hostFilePath);
                var linkFiles = rvtFiles.Where(f =>
                    !f.Equals(hostFilePath, StringComparison.OrdinalIgnoreCase) &&
                    !Path.GetFileName(f).Equals("input.rvt", StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                LoggingService.Log($"Found {linkFiles.Length} Revit files to link");

                int linkedCount = 0;

                using (Transaction tx = new Transaction(doc, "Link Revit Files"))
                {
                    tx.Start();

                    foreach (string linkFilePath in linkFiles)
                    {
                        try
                        {
                            LoggingService.Log($"Linking file: {Path.GetFileName(linkFilePath)}");

                            // Create a Revit link type and instance
                            RevitLinkInstance linkInstance = CreateRevitLink(doc, linkFilePath);
                            
                            // Pin the link instance
                            PinLinkInstance(doc, linkInstance);
                            
                            linkedCount++;
                            LoggingService.Log($"Successfully linked and pinned: {Path.GetFileName(linkFilePath)}");
                        }
                        catch (Exception ex)
                        {
                            LoggingService.LogError($"Error linking file {Path.GetFileName(linkFilePath)}: {ex.Message}");
                        }
                    }

                    tx.Commit();
                }

                LoggingService.Log($"Successfully linked {linkedCount} of {linkFiles.Length} Revit files");
                return linkedCount;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error in LinkRevitFiles: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Creates a Revit link type and instance
        /// </summary>
        /// <param name="doc">The host document</param>
        /// <param name="linkFilePath">The path to the Revit file to link</param>
        /// <returns>The RevitLinkInstance if successful</returns>
        private static RevitLinkInstance CreateRevitLink(Document doc, string linkFilePath)
        {
            try
            {
                // Create a RevitLinkType
                var result = RevitLinkType.Create(doc, ModelPathUtils.ConvertUserVisiblePathToModelPath(linkFilePath), new RevitLinkOptions(true));
                var linkTypeId = result.ElementId;

                if (linkTypeId != null)
                {
                    // Create a link instance
                    RevitLinkInstance linkInstance = RevitLinkInstance.Create(doc, linkTypeId);
                    return linkInstance;
                }
                else
                {
                    var message = $"Could not create a link from the file at: {linkFilePath}";
                    throw new Exception(message);
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error creating Revit link {Path.GetFileName(linkFilePath)}: {ex.Message}");
                throw ex;
            }
        }
        
        /// <summary>
        /// Pins a Revit link instance
        /// </summary>
        /// <param name="doc">The host document</param>
        /// <param name="linkInstance">The Revit link instance to pin</param>
        private static void PinLinkInstance(Document doc, RevitLinkInstance linkInstance)
        {
            try
            {
                // Check if the link is already pinned
                if (!linkInstance.Pinned)
                {
                    // Pin the link
                    linkInstance.Pinned = true;
                    LoggingService.Log($"Pinned link instance: {linkInstance.Id}");
                }
                else
                {
                    LoggingService.Log($"Link instance already pinned: {linkInstance.Id}");
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error pinning link instance {linkInstance.Id}: {ex.Message}");
                throw ex;
            }
        }
    }
}