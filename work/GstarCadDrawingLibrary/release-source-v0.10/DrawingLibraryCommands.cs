using System;
using System.IO;
using System.Windows.Forms;
using Gssoft.Gscad.ApplicationServices;
using Gssoft.Gscad.Runtime;
using Gssoft.Gscad.Geometry;
using GcadApplication = Gssoft.Gscad.ApplicationServices.Application;

namespace GstarCadDrawingLibrary
{
    public sealed class DrawingLibraryCommands
    {
        [CommandMethod("DLIB")]
        public void ShowDrawingLibrary()
        {
            PluginEntry.ShowPalette();
        }

        [CommandMethod("DLREFRESH")]
        public void RefreshDrawingLibrary()
        {
            PluginEntry.RefreshPalette();
        }

        [CommandMethod("DLSETROOT")]
        public void SetDrawingLibraryRoot()
        {
            PluginEntry.ChooseRootDirectory();
        }

        [CommandMethod("DLINSERT")]
        public void InsertDrawing()
        {
            InsertDrawingEventArgs request = PluginEntry.TakePendingInsertion();
            if (request == null)
            {
                using (OpenFileDialog dialog = new OpenFileDialog())
                {
                    dialog.Title = "选择要插入的 DWG";
                    dialog.Filter = "DWG 图纸 (*.dwg)|*.dwg";
                    if (dialog.ShowDialog() != DialogResult.OK)
                    {
                        return;
                    }

                    request = new InsertDrawingEventArgs(dialog.FileName, 1D, 0D);
                }
            }

            Document document = GcadApplication.DocumentManager.MdiActiveDocument;
            if (document == null)
            {
                return;
            }

            DwgInsertService.Insert(document, request);
        }

        [CommandMethod("DLSELFTEST")]
        public void RunSelfTest()
        {
            Document document = GcadApplication.DocumentManager.MdiActiveDocument;
            string resultPath = Environment.GetEnvironmentVariable("GSTAR_DLIB_SELFTEST");

            try
            {
                PluginEntry.ShowPalette();
                string insertionResult = "NO_INSERT_TEST";
                string testDrawing = Environment.GetEnvironmentVariable("GSTAR_DLIB_TEST_DWG");
                if (document != null && !string.IsNullOrWhiteSpace(testDrawing) && File.Exists(testDrawing))
                {
                    DwgInsertService.InsertAt(
                        document,
                        new InsertDrawingEventArgs(testDrawing, 1D, 0D),
                        new Point3d(0D, 0D, 0D));
                    insertionResult = "INSERT_PASS";
                }

                string result = "PASS|" + DateTime.Now.ToString("O") + "|"
                    + (document == null ? "NO_DOCUMENT" : document.Name) + "|" + insertionResult;
                if (!string.IsNullOrWhiteSpace(resultPath))
                {
                    File.WriteAllText(resultPath, result);
                }

                if (document != null)
                {
                    document.Editor.WriteMessage("\n图纸库自检通过。\n");
                }
            }
            catch (System.Exception ex)
            {
                if (!string.IsNullOrWhiteSpace(resultPath))
                {
                    File.WriteAllText(resultPath, "FAIL|" + ex);
                }

                throw;
            }
        }
    }
}
