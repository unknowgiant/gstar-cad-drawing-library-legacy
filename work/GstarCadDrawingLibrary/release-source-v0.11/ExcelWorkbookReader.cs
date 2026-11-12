using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;

namespace GstarCadDrawingLibrary
{
    internal sealed class ExcelSheetData
    {
        public ExcelSheetData(string name, string[,] values)
        {
            Name = name;
            Values = values;
        }

        public string Name { get; private set; }
        public string[,] Values { get; private set; }
        public int RowCount { get { return Values.GetLength(0); } }
        public int ColumnCount { get { return Values.GetLength(1); } }
    }

    internal static class ExcelWorkbookReader
    {
        private const int MaximumCellCount = 50000;

        public static IList<ExcelSheetData> Read(string filePath)
        {
            Type excelType = Type.GetTypeFromProgID("Excel.Application");
            if (excelType == null)
            {
                throw new InvalidOperationException("未检测到 Microsoft Excel，无法读取工作簿。");
            }

            List<ExcelSheetData> sheets = new List<ExcelSheetData>();
            object application = null;
            object workbooks = null;
            object workbook = null;
            object worksheets = null;
            try
            {
                application = Activator.CreateInstance(excelType);
                SetProperty(application, "Visible", false);
                SetProperty(application, "DisplayAlerts", false);
                workbooks = GetProperty(application, "Workbooks");
                workbook = InvokeMethod(workbooks, "Open", filePath, 0, true);
                worksheets = GetProperty(workbook, "Worksheets");
                int sheetCount = Convert.ToInt32(GetProperty(worksheets, "Count"), CultureInfo.InvariantCulture);
                for (int index = 1; index <= sheetCount; index++)
                {
                    object worksheet = null;
                    object usedRange = null;
                    object rows = null;
                    object columns = null;
                    try
                    {
                        worksheet = GetProperty(worksheets, "Item", index);
                        string name = Convert.ToString(GetProperty(worksheet, "Name"), CultureInfo.CurrentCulture);
                        usedRange = GetProperty(worksheet, "UsedRange");
                        rows = GetProperty(usedRange, "Rows");
                        columns = GetProperty(usedRange, "Columns");
                        int rowCount = Convert.ToInt32(GetProperty(rows, "Count"), CultureInfo.InvariantCulture);
                        int columnCount = Convert.ToInt32(GetProperty(columns, "Count"), CultureInfo.InvariantCulture);
                        if ((long)rowCount * columnCount > MaximumCellCount)
                        {
                            throw new InvalidOperationException(
                                "工作表“" + name + "”的使用区域超过 " + MaximumCellCount + " 个单元格，请先在 Excel 中缩小使用区域。");
                        }

                        object rawValues = GetProperty(usedRange, "Value2");
                        string[,] values = ConvertValues(rawValues, rowCount, columnCount);
                        values = TrimEmptyEdges(values);
                        if (values.GetLength(0) > 0 && values.GetLength(1) > 0)
                        {
                            sheets.Add(new ExcelSheetData(name, values));
                        }
                    }
                    finally
                    {
                        ReleaseComObject(columns);
                        ReleaseComObject(rows);
                        ReleaseComObject(usedRange);
                        ReleaseComObject(worksheet);
                    }
                }
            }
            catch (TargetInvocationException ex)
            {
                throw new InvalidOperationException(
                    "读取 Excel 失败：" + (ex.InnerException == null ? ex.Message : ex.InnerException.Message),
                    ex);
            }
            finally
            {
                if (workbook != null)
                {
                    try
                    {
                        InvokeMethod(workbook, "Close", false);
                    }
                    catch
                    {
                    }
                }

                if (application != null)
                {
                    try
                    {
                        InvokeMethod(application, "Quit");
                    }
                    catch
                    {
                    }
                }

                ReleaseComObject(worksheets);
                ReleaseComObject(workbook);
                ReleaseComObject(workbooks);
                ReleaseComObject(application);
            }

            return sheets;
        }

        private static string[,] ConvertValues(object rawValues, int rowCount, int columnCount)
        {
            string[,] values = new string[rowCount, columnCount];
            Array array = rawValues as Array;
            if (array != null && array.Rank == 2)
            {
                int firstRow = array.GetLowerBound(0);
                int firstColumn = array.GetLowerBound(1);
                for (int row = 0; row < rowCount; row++)
                {
                    for (int column = 0; column < columnCount; column++)
                    {
                        values[row, column] = FormatValue(array.GetValue(firstRow + row, firstColumn + column));
                    }
                }
            }
            else if (rowCount > 0 && columnCount > 0)
            {
                values[0, 0] = FormatValue(rawValues);
            }

            return values;
        }

        private static string[,] TrimEmptyEdges(string[,] values)
        {
            int lastRow = -1;
            int lastColumn = -1;
            for (int row = 0; row < values.GetLength(0); row++)
            {
                for (int column = 0; column < values.GetLength(1); column++)
                {
                    if (!string.IsNullOrWhiteSpace(values[row, column]))
                    {
                        lastRow = Math.Max(lastRow, row);
                        lastColumn = Math.Max(lastColumn, column);
                    }
                }
            }

            if (lastRow < 0 || lastColumn < 0)
            {
                return new string[0, 0];
            }

            string[,] result = new string[lastRow + 1, lastColumn + 1];
            for (int row = 0; row <= lastRow; row++)
            {
                for (int column = 0; column <= lastColumn; column++)
                {
                    result[row, column] = values[row, column] ?? string.Empty;
                }
            }

            return result;
        }

        private static string FormatValue(object value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            double number;
            if (value is double)
            {
                number = (double)value;
                return number.ToString("G15", CultureInfo.CurrentCulture);
            }

            return Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty;
        }

        private static object GetProperty(object target, string name, params object[] arguments)
        {
            return target.GetType().InvokeMember(
                name,
                BindingFlags.GetProperty,
                null,
                target,
                arguments == null || arguments.Length == 0 ? null : arguments,
                CultureInfo.CurrentCulture);
        }

        private static void SetProperty(object target, string name, object value)
        {
            target.GetType().InvokeMember(
                name,
                BindingFlags.SetProperty,
                null,
                target,
                new object[] { value },
                CultureInfo.CurrentCulture);
        }

        private static object InvokeMethod(object target, string name, params object[] arguments)
        {
            return target.GetType().InvokeMember(
                name,
                BindingFlags.InvokeMethod,
                null,
                target,
                arguments == null || arguments.Length == 0 ? null : arguments,
                CultureInfo.CurrentCulture);
        }

        private static void ReleaseComObject(object value)
        {
            if (value != null && Marshal.IsComObject(value))
            {
                try
                {
                    Marshal.FinalReleaseComObject(value);
                }
                catch
                {
                }
            }
        }
    }
}
