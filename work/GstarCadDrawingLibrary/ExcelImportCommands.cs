using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Gssoft.Gscad.ApplicationServices;
using Gssoft.Gscad.DatabaseServices;
using Gssoft.Gscad.EditorInput;
using Gssoft.Gscad.Geometry;
using Gssoft.Gscad.Runtime;
using GcadApplication = Gssoft.Gscad.ApplicationServices.Application;

namespace GstarCadDrawingLibrary
{
    public sealed class ExcelImportCommands
    {
        [CommandMethod("DLEXCEL")]
        public void InsertExcelTable()
        {
            Document document = GcadApplication.DocumentManager.MdiActiveDocument;
            if (document == null)
            {
                return;
            }

            try
            {
                string filePath;
                using (OpenFileDialog dialog = new OpenFileDialog())
                {
                    dialog.Title = "选择要插入的 Excel 表格";
                    dialog.Filter = "Excel 工作簿 (*.xlsx;*.xls)|*.xlsx;*.xls|CSV 文件 (*.csv)|*.csv|全部支持文件|*.xlsx;*.xls;*.csv";
                    dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    dialog.Multiselect = false;
                    if (dialog.ShowDialog() != DialogResult.OK)
                    {
                        return;
                    }

                    filePath = dialog.FileName;
                }

                document.Editor.WriteMessage("\n正在读取 Excel，请稍候...\n");
                IList<ExcelSheetData> sheets = ExcelWorkbookReader.Read(filePath);
                if (sheets.Count == 0)
                {
                    document.Editor.WriteMessage("\n工作簿中没有可插入的非空工作表。\n");
                    return;
                }

                ExcelSheetData selectedSheet;
                if (!ExcelWorksheetSelectionDialog.TrySelect(null, sheets, out selectedSheet))
                {
                    return;
                }

                ObjectId tableId;
                if (!TryInsertTable(document, selectedSheet, out tableId))
                {
                    return;
                }

                document.Editor.SetImpliedSelection(new ObjectId[] { tableId });
                document.Editor.WriteMessage(
                    "\n已插入工作表“" + selectedSheet.Name + "”："
                    + selectedSheet.RowCount + " 行 × " + selectedSheet.ColumnCount + " 列。\n");
            }
            catch (System.Exception ex)
            {
                document.Editor.WriteMessage("\nExcel 插入失败：" + ex.Message + "\n");
            }
        }

        [CommandMethod("DLEXCELPASTE")]
        public void PasteExcelClipboard()
        {
            Document document = GcadApplication.DocumentManager.MdiActiveDocument;
            if (document == null)
            {
                return;
            }

            try
            {
                string clipboardText = GetClipboardText();
                ExcelSheetData sheet = ExcelClipboardParser.Parse(clipboardText);
                ObjectId tableId;
                if (!TryInsertTable(document, sheet, out tableId))
                {
                    return;
                }

                document.Editor.SetImpliedSelection(new ObjectId[] { tableId });
                document.Editor.WriteMessage(
                    "\n已将 Excel 剪贴板插入为原生表格："
                    + sheet.RowCount + " 行 × " + sheet.ColumnCount + " 列。\n");
            }
            catch (System.Exception ex)
            {
                document.Editor.WriteMessage("\n粘贴 Excel 表格失败：" + ex.Message + "\n");
            }
        }

        private static bool TryInsertTable(Document document, ExcelSheetData sheet, out ObjectId tableId)
        {
            tableId = ObjectId.Null;
            PromptPointResult pointResult = document.Editor.GetPoint("\n指定表格左上角插入点：");
            if (pointResult.Status != PromptStatus.OK)
            {
                return false;
            }

            PromptDoubleOptions heightOptions = new PromptDoubleOptions("\n输入表格文字高度 <2.5>：");
            heightOptions.AllowNegative = false;
            heightOptions.AllowZero = false;
            heightOptions.AllowNone = true;
            heightOptions.DefaultValue = 2.5D;
            heightOptions.UseDefaultValue = true;
            PromptDoubleResult heightResult = document.Editor.GetDouble(heightOptions);
            if (heightResult.Status != PromptStatus.OK && heightResult.Status != PromptStatus.None)
            {
                return false;
            }

            double textHeight = heightResult.Status == PromptStatus.OK ? heightResult.Value : 2.5D;
            tableId = InsertTable(document, sheet, pointResult.Value, textHeight);
            return true;
        }

        private static string GetClipboardText()
        {
            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            {
                return ReadClipboardWithRetry();
            }

            string result = null;
            System.Exception error = null;
            Thread thread = new Thread(delegate()
            {
                try
                {
                    result = ReadClipboardWithRetry();
                }
                catch (System.Exception ex)
                {
                    error = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (error != null)
            {
                throw error;
            }

            return result;
        }

        private static string ReadClipboardWithRetry()
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    return Clipboard.GetText(TextDataFormat.UnicodeText);
                }
                catch (ExternalException)
                {
                    if (attempt == 2)
                    {
                        throw new InvalidOperationException("剪贴板正被其他程序占用，请重新复制后再试。");
                    }

                    Thread.Sleep(80);
                }
            }

            return string.Empty;
        }

        private static ObjectId InsertTable(
            Document document,
            ExcelSheetData sheet,
            Point3d insertionPoint,
            double textHeight)
        {
            Database database = document.Database;
            using (Transaction transaction = database.TransactionManager.StartTransaction())
            {
                Table table = new Table();
                table.TableStyle = database.Tablestyle;
                table.Position = insertionPoint;
                table.SetSize(sheet.RowCount, sheet.ColumnCount);
                table.SetRowHeight(textHeight * 1.8D);
                table.Cells.TextHeight = textHeight;

                for (int column = 0; column < sheet.ColumnCount; column++)
                {
                    int maximumLength = 0;
                    for (int row = 0; row < sheet.RowCount; row++)
                    {
                        maximumLength = Math.Max(maximumLength, GetDisplayLength(sheet.Values[row, column]));
                    }

                    double width = textHeight * Math.Max(6D, Math.Min(30D, maximumLength + 2D)) * 0.8D;
                    table.Columns[column].Width = width;
                }

                for (int row = 0; row < sheet.RowCount; row++)
                {
                    for (int column = 0; column < sheet.ColumnCount; column++)
                    {
                        table.Cells[row, column].TextString = EscapeTableText(sheet.Values[row, column]);
                    }
                }

                table.GenerateLayout();
                BlockTableRecord currentSpace = (BlockTableRecord)transaction.GetObject(
                    database.CurrentSpaceId,
                    OpenMode.ForWrite);
                ObjectId tableId = currentSpace.AppendEntity(table);
                transaction.AddNewlyCreatedDBObject(table, true);
                table.RecomputeTableBlock(true);
                transaction.Commit();
                return tableId;
            }
        }

        private static int GetDisplayLength(string value)
        {
            return string.IsNullOrEmpty(value)
                ? 0
                : StringInfo.ParseCombiningCharacters(value.Replace("\r", string.Empty).Replace("\n", string.Empty)).Length;
        }

        private static string EscapeTableText(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("{", "\\{")
                .Replace("}", "\\}")
                .Replace("\r\n", "\\P")
                .Replace("\n", "\\P")
                .Replace("\r", "\\P");
        }
    }
}
