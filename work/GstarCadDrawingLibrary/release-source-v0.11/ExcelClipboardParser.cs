using System;
using System.Collections.Generic;
using System.Text;

namespace GstarCadDrawingLibrary
{
    internal static class ExcelClipboardParser
    {
        private const int MaximumCellCount = 50000;

        public static ExcelSheetData Parse(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                throw new InvalidOperationException("剪贴板中没有可粘贴的 Excel 单元格。");
            }

            List<List<string>> rows = new List<List<string>>();
            List<string> currentRow = new List<string>();
            StringBuilder currentValue = new StringBuilder();
            bool quoted = false;
            bool atFieldStart = true;
            bool rowJustCompleted = false;
            int maximumColumnCount = 0;

            for (int index = 0; index < text.Length; index++)
            {
                char character = text[index];
                if (quoted)
                {
                    rowJustCompleted = false;
                    if (character == '"')
                    {
                        if (index + 1 < text.Length && text[index + 1] == '"')
                        {
                            currentValue.Append('"');
                            index++;
                        }
                        else
                        {
                            quoted = false;
                        }
                    }
                    else
                    {
                        currentValue.Append(character);
                    }

                    continue;
                }

                if (atFieldStart && character == '"')
                {
                    rowJustCompleted = false;
                    quoted = true;
                    atFieldStart = false;
                }
                else if (character == '\t')
                {
                    rowJustCompleted = false;
                    AddField(currentRow, currentValue);
                    atFieldStart = true;
                }
                else if (character == '\r' || character == '\n')
                {
                    if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                    {
                        index++;
                    }

                    AddField(currentRow, currentValue);
                    AddRow(rows, currentRow);
                    maximumColumnCount = Math.Max(maximumColumnCount, currentRow.Count);
                    ValidateSize(rows.Count, maximumColumnCount);
                    currentRow = new List<string>();
                    atFieldStart = true;
                    rowJustCompleted = true;
                }
                else
                {
                    rowJustCompleted = false;
                    currentValue.Append(character);
                    atFieldStart = false;
                }
            }

            if (!rowJustCompleted)
            {
                AddField(currentRow, currentValue);
                AddRow(rows, currentRow);
                maximumColumnCount = Math.Max(maximumColumnCount, currentRow.Count);
                ValidateSize(rows.Count, maximumColumnCount);
            }

            return CreateSheet(rows);
        }

        private static void AddField(List<string> row, StringBuilder value)
        {
            row.Add(value.ToString());
            value.Length = 0;
            if (row.Count > MaximumCellCount)
            {
                throw CreateSizeException();
            }
        }

        private static void AddRow(List<List<string>> rows, List<string> row)
        {
            rows.Add(row);
        }

        private static ExcelSheetData CreateSheet(List<List<string>> rows)
        {
            int lastRow = -1;
            int lastColumn = -1;
            for (int row = 0; row < rows.Count; row++)
            {
                for (int column = 0; column < rows[row].Count; column++)
                {
                    if (!string.IsNullOrWhiteSpace(rows[row][column]))
                    {
                        lastRow = Math.Max(lastRow, row);
                        lastColumn = Math.Max(lastColumn, column);
                    }
                }
            }

            if (lastRow < 0 || lastColumn < 0)
            {
                throw new InvalidOperationException("剪贴板中的 Excel 区域是空白的。");
            }

            if ((long)(lastRow + 1) * (lastColumn + 1) > MaximumCellCount)
            {
                throw CreateSizeException();
            }

            string[,] values = new string[lastRow + 1, lastColumn + 1];
            for (int row = 0; row <= lastRow; row++)
            {
                for (int column = 0; column <= lastColumn; column++)
                {
                    values[row, column] = column < rows[row].Count ? rows[row][column] : string.Empty;
                }
            }

            return new ExcelSheetData("Excel 剪贴板", values);
        }

        private static void ValidateSize(int rowCount, int columnCount)
        {
            if ((long)rowCount * columnCount > MaximumCellCount)
            {
                throw CreateSizeException();
            }
        }

        private static InvalidOperationException CreateSizeException()
        {
            return new InvalidOperationException("复制的 Excel 区域超过 50000 个单元格，请缩小选择范围后重试。");
        }
    }
}
