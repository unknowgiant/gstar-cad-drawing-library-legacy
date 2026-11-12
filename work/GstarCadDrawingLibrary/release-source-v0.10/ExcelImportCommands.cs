using System;
using System.Collections.Generic;
using System.Globalization;
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

                PromptPointResult pointResult = document.Editor.GetPoint("\n指定表格左上角插入点：");
                if (pointResult.Status != PromptStatus.OK)
                {
                    return;
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
                    return;
                }

                double textHeight = heightResult.Status == PromptStatus.OK ? heightResult.Value : 2.5D;
                ObjectId tableId = InsertTable(document, selectedSheet, pointResult.Value, textHeight);
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
