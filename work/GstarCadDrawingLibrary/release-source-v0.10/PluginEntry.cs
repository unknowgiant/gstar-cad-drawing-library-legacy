using System;
using System.Collections.Generic;
using System.Drawing;
using Gssoft.Gscad.ApplicationServices;
using Gssoft.Gscad.Runtime;
using Gssoft.Gscad.Windows;
using GcadApplication = Gssoft.Gscad.ApplicationServices.Application;

namespace GstarCadDrawingLibrary
{
    public sealed class PluginEntry : IExtensionApplication
    {
        private static readonly Guid PaletteId = new Guid("A78645AA-DDF3-4B0D-AEC3-FCCEC769D39D");
        private static PaletteSet palette;
        private static DrawingLibraryControl control;
        private static CadToolsControl toolsControl;
        private static readonly Queue<InsertDrawingEventArgs> PendingInsertions =
            new Queue<InsertDrawingEventArgs>();

        public void Initialize()
        {
            Document document = GcadApplication.DocumentManager.MdiActiveDocument;
            if (document != null)
            {
                document.Editor.WriteMessage("\n浩辰图纸库已加载。输入 DLIB 打开图库。\n");
            }
        }

        public void Terminate()
        {
            if (palette != null)
            {
                palette.Dispose();
                palette = null;
                control = null;
                toolsControl = null;
            }

            lock (PendingInsertions)
            {
                PendingInsertions.Clear();
            }
        }

        internal static void ShowPalette()
        {
            EnsurePalette();
            palette.Visible = true;
        }

        internal static void RefreshPalette()
        {
            EnsurePalette();
            control.ReloadLibrary();
            palette.Visible = true;
        }

        internal static void ChooseRootDirectory()
        {
            EnsurePalette();
            control.ChooseRootDirectory();
            palette.Visible = true;
        }

        internal static InsertDrawingEventArgs TakePendingInsertion()
        {
            lock (PendingInsertions)
            {
                return PendingInsertions.Count == 0 ? null : PendingInsertions.Dequeue();
            }
        }

        private static void EnsurePalette()
        {
            if (palette != null)
            {
                return;
            }

            control = new DrawingLibraryControl();
            control.InsertRequested += OnInsertRequested;
            toolsControl = new CadToolsControl();

            palette = new PaletteSet("图纸库", PaletteId);
            palette.Style = PaletteSetStyles.ShowAutoHideButton
                | PaletteSetStyles.ShowCloseButton
                | PaletteSetStyles.Snappable;
            palette.DockEnabled = DockSides.Left | DockSides.Right;
            palette.Dock = DockSides.Right;
            palette.Size = new Size(380, 650);
            palette.Add("图纸库", control);
            palette.Add("辅助工具", toolsControl);
        }

        private static void OnInsertRequested(object sender, InsertDrawingEventArgs e)
        {
            Document document = GcadApplication.DocumentManager.MdiActiveDocument;
            if (document == null)
            {
                return;
            }

            lock (PendingInsertions)
            {
                PendingInsertions.Enqueue(e);
            }
            document.SendStringToExecute("DLINSERT ", true, false, false);
        }
    }
}
