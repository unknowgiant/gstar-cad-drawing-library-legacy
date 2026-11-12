using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Gssoft.Gscad.ApplicationServices;
using Gssoft.Gscad.DatabaseServices;
using Gssoft.Gscad.EditorInput;
using Gssoft.Gscad.Geometry;
using Gssoft.Gscad.Runtime;
using GcadApplication = Gssoft.Gscad.ApplicationServices.Application;

namespace GstarCadDrawingLibrary
{
    public sealed class TextAndStatisticsCommands
    {
        private static readonly Regex NumberPattern = new Regex(
            "[-+]?(?:\\d+(?:\\.\\d*)?|\\.\\d+)",
            RegexOptions.Compiled);

        [CommandMethod("DLTEXTPREFIX")]
        public void AddTextPrefix()
        {
            Run(delegate(Document document)
            {
                string value;
                if (!TryGetString(document.Editor, "\n输入要添加的文字前缀：", false, out value))
                {
                    return;
                }

                ModifySelectedText(
                    document,
                    "选择要添加前缀的文字",
                    delegate(DBText text) { text.TextString = value + text.TextString; },
                    delegate(MText text) { text.Contents = value + text.Contents; });
            });
        }

        [CommandMethod("DLTEXTSUFFIX")]
        public void AddTextSuffix()
        {
            Run(delegate(Document document)
            {
                string value;
                if (!TryGetString(document.Editor, "\n输入要添加的文字后缀：", false, out value))
                {
                    return;
                }

                ModifySelectedText(
                    document,
                    "选择要添加后缀的文字",
                    delegate(DBText text) { text.TextString = text.TextString + value; },
                    delegate(MText text) { text.Contents = text.Contents + value; });
            });
        }

        [CommandMethod("DLTEXTHEIGHT")]
        public void SetTextHeight()
        {
            Run(delegate(Document document)
            {
                double value;
                if (!TryGetPositiveDouble(document.Editor, "\n输入新的文字高度：", out value))
                {
                    return;
                }

                ModifySelectedText(
                    document,
                    "选择要修改高度的文字",
                    delegate(DBText text) { text.Height = value; },
                    delegate(MText text) { text.TextHeight = value; });
            });
        }

        [CommandMethod("DLTEXTWIDTH")]
        public void SetTextWidthFactor()
        {
            Run(delegate(Document document)
            {
                double value;
                if (!TryGetPositiveDouble(document.Editor, "\n输入新的单行文字宽度系数：", out value))
                {
                    return;
                }

                ModifySelectedText(
                    document,
                    "选择要修改宽度系数的单行文字",
                    delegate(DBText text) { text.WidthFactor = value; },
                    null);
            });
        }

        [CommandMethod("DLTEXTSWAP")]
        public void SwapTextPositions()
        {
            Run(delegate(Document document)
            {
                Editor editor = document.Editor;
                int firstPosition;
                if (!TryGetPositiveInteger(editor, "\n输入第一个字符位置（从 1 开始）：", false, 1, out firstPosition))
                {
                    return;
                }

                int secondPosition;
                if (!TryGetPositiveInteger(editor, "\n输入第二个字符位置（从 1 开始）：", false, 1, out secondPosition))
                {
                    return;
                }

                int length;
                if (!TryGetPositiveInteger(editor, "\n输入互换字符长度 <1>：", true, 1, out length))
                {
                    return;
                }

                if (firstPosition == secondPosition)
                {
                    editor.WriteMessage("\n两个位置相同，无需互换。\n");
                    return;
                }

                if (Math.Abs(firstPosition - secondPosition) < length)
                {
                    editor.WriteMessage("\n两个字符组存在重叠，请调整位置或互换长度。\n");
                    return;
                }

                ObjectId[] selectedIds = GetSelection(document, "选择要互换字符位置的文字", true);
                if (selectedIds == null)
                {
                    return;
                }

                int changed = 0;
                int outOfRange = 0;
                int formattedMText = 0;
                int nonText = 0;
                using (Transaction transaction = document.Database.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId objectId in selectedIds)
                    {
                        string dxfName = objectId.ObjectClass.DxfName;
                        if (!string.Equals(dxfName, "TEXT", StringComparison.OrdinalIgnoreCase)
                            && !string.Equals(dxfName, "MTEXT", StringComparison.OrdinalIgnoreCase))
                        {
                            nonText++;
                            continue;
                        }

                        DBObject value = transaction.GetObject(objectId, OpenMode.ForWrite, false);
                        DBText dbText = value as DBText;
                        MText mText = value as MText;
                        string original = dbText != null ? dbText.TextString : mText.Contents;
                        if (mText != null && HasMTextFormatting(original))
                        {
                            formattedMText++;
                            continue;
                        }

                        string swapped;
                        if (!TrySwapSegments(original, firstPosition, secondPosition, length, out swapped))
                        {
                            outOfRange++;
                            continue;
                        }

                        if (dbText != null)
                        {
                            dbText.TextString = swapped;
                        }
                        else
                        {
                            mText.Contents = swapped;
                        }

                        changed++;
                    }

                    transaction.Commit();
                }

                editor.Regen();
                editor.WriteMessage(
                    "\n字符位置互换完成：成功 " + changed
                    + "，长度不足 " + outOfRange
                    + "，带格式多行文字 " + formattedMText
                    + "，非文字 " + nonText + "。\n");
            });
        }

        [CommandMethod("DLTEXTSWAPCONTENT")]
        public void SwapTextContents()
        {
            Run(delegate(Document document)
            {
                ObjectId firstId;
                if (!TryGetTextEntity(document, "\n选择第一段文字：", out firstId))
                {
                    return;
                }

                ObjectId secondId;
                if (!TryGetTextEntity(document, "\n选择第二段文字：", out secondId))
                {
                    return;
                }

                if (firstId == secondId)
                {
                    document.Editor.WriteMessage("\n选择的是同一个文字对象，无需互换。\n");
                    return;
                }

                using (Transaction transaction = document.Database.TransactionManager.StartTransaction())
                {
                    Entity first = transaction.GetObject(firstId, OpenMode.ForWrite, false) as Entity;
                    Entity second = transaction.GetObject(secondId, OpenMode.ForWrite, false) as Entity;
                    string firstText = GetTextContent(first);
                    string secondText = GetTextContent(second);
                    bool sameType = first.GetType() == second.GetType();
                    if (!sameType && (HasMTextFormatting(firstText) || HasMTextFormatting(secondText)))
                    {
                        document.Editor.WriteMessage("\n带格式多行文字只能与同类型多行文字互换内容。\n");
                        return;
                    }

                    SetTextContent(first, secondText);
                    SetTextContent(second, firstText);
                    transaction.Commit();
                }

                document.Editor.Regen();
                document.Editor.WriteMessage("\n已互换两段文字的内容。\n");
            });
        }

        [CommandMethod("DLTEXTSWAPPLACE")]
        public void SwapTextPlacements()
        {
            Run(delegate(Document document)
            {
                ObjectId firstId;
                if (!TryGetTextEntity(document, "\n选择第一段文字：", out firstId))
                {
                    return;
                }

                ObjectId secondId;
                if (!TryGetTextEntity(document, "\n选择第二段文字：", out secondId))
                {
                    return;
                }

                if (firstId == secondId)
                {
                    document.Editor.WriteMessage("\n选择的是同一个文字对象，无需互换。\n");
                    return;
                }

                using (Transaction transaction = document.Database.TransactionManager.StartTransaction())
                {
                    Entity first = transaction.GetObject(firstId, OpenMode.ForWrite, false) as Entity;
                    Entity second = transaction.GetObject(secondId, OpenMode.ForWrite, false) as Entity;
                    Point3d firstPoint = GetTextAnchor(first);
                    Point3d secondPoint = GetTextAnchor(second);
                    first.TransformBy(Matrix3d.Displacement(firstPoint.GetVectorTo(secondPoint)));
                    second.TransformBy(Matrix3d.Displacement(secondPoint.GetVectorTo(firstPoint)));
                    transaction.Commit();
                }

                document.Editor.Regen();
                document.Editor.WriteMessage("\n已互换两段文字的位置。\n");
            });
        }

        [CommandMethod("DLTEXTNUMBER")]
        public void NumberText()
        {
            Run(delegate(Document document)
            {
                Editor editor = document.Editor;
                ObjectId[] selectedIds = GetSelection(document, "选择要递增编号的文字", true);
                if (selectedIds == null)
                {
                    return;
                }

                string prefix;
                if (!TryGetString(editor, "\n输入编号前缀（可留空）：", true, out prefix))
                {
                    return;
                }

                int start;
                if (!TryGetInteger(editor, "\n输入起始编号：", 1, out start))
                {
                    return;
                }

                int step;
                if (!TryGetInteger(editor, "\n输入递增步长：", 1, out step))
                {
                    return;
                }

                List<TextTarget> targets = new List<TextTarget>();
                using (Transaction transaction = document.Database.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId objectId in selectedIds)
                    {
                        DBObject value = transaction.GetObject(objectId, OpenMode.ForRead, false);
                        DBText dbText = value as DBText;
                        MText mText = value as MText;
                        if (dbText != null)
                        {
                            targets.Add(new TextTarget(objectId, dbText.Position));
                        }
                        else if (mText != null)
                        {
                            targets.Add(new TextTarget(objectId, mText.Location));
                        }
                    }

                    transaction.Commit();
                }

                targets.Sort(CompareTextTargets);
                using (Transaction transaction = document.Database.TransactionManager.StartTransaction())
                {
                    int number = start;
                    foreach (TextTarget target in targets)
                    {
                        DBObject value = transaction.GetObject(target.ObjectId, OpenMode.ForWrite, false);
                        string text = prefix + number.ToString(CultureInfo.CurrentCulture);
                        DBText dbText = value as DBText;
                        MText mText = value as MText;
                        if (dbText != null)
                        {
                            dbText.TextString = text;
                        }
                        else if (mText != null)
                        {
                            mText.Contents = text;
                        }

                        number += step;
                    }

                    transaction.Commit();
                }

                editor.Regen();
                editor.WriteMessage("\n已完成 " + targets.Count + " 个文字的递增编号。\n");
            });
        }

        [CommandMethod("DLBLOCKCOUNT")]
        public void CountBlocks()
        {
            Run(delegate(Document document)
            {
                ObjectId[] selectedIds = GetSelection(document, "选择要统计的块参照", false);
                if (selectedIds == null)
                {
                    return;
                }

                Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.CurrentCultureIgnoreCase);
                using (Transaction transaction = document.Database.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId objectId in selectedIds)
                    {
                        BlockReference reference = transaction.GetObject(objectId, OpenMode.ForRead, false) as BlockReference;
                        if (reference == null)
                        {
                            continue;
                        }

                        string name = GetEffectiveBlockName(reference, transaction);
                        int count;
                        counts.TryGetValue(name, out count);
                        counts[name] = count + 1;
                    }

                    transaction.Commit();
                }

                int total = counts.Values.Sum();
                document.Editor.WriteMessage("\n块数量统计：共 " + total + " 个，" + counts.Count + " 种。\n");
                foreach (KeyValuePair<string, int> item in counts.OrderByDescending(delegate(KeyValuePair<string, int> pair) { return pair.Value; })
                    .ThenBy(delegate(KeyValuePair<string, int> pair) { return pair.Key; }, StringComparer.CurrentCultureIgnoreCase))
                {
                    document.Editor.WriteMessage("  " + item.Key + "：" + item.Value + "\n");
                }
            });
        }

        [CommandMethod("DLLENGTHSUM")]
        public void SumLengths()
        {
            Run(delegate(Document document)
            {
                ObjectId[] selectedIds = GetSelection(document, "选择要统计长度的曲线", false);
                if (selectedIds == null)
                {
                    return;
                }

                int count = 0;
                double total = 0D;
                using (Transaction transaction = document.Database.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId objectId in selectedIds)
                    {
                        Curve curve = transaction.GetObject(objectId, OpenMode.ForRead, false) as Curve;
                        if (curve == null)
                        {
                            continue;
                        }

                        try
                        {
                            double startDistance = curve.GetDistanceAtParameter(curve.StartParam);
                            double endDistance = curve.GetDistanceAtParameter(curve.EndParam);
                            total += Math.Abs(endDistance - startDistance);
                            count++;
                        }
                        catch
                        {
                            // Unsupported or invalid curves are omitted from the total.
                        }
                    }

                    transaction.Commit();
                }

                document.Editor.WriteMessage(
                    "\n长度统计：" + count + " 个曲线对象，总长度 = "
                    + total.ToString("0.###", CultureInfo.CurrentCulture) + "。\n");
            });
        }

        [CommandMethod("DLAREASUM")]
        public void SumAreas()
        {
            Run(delegate(Document document)
            {
                ObjectId[] selectedIds = GetSelection(document, "选择要统计面积的封闭曲线、面域或填充", false);
                if (selectedIds == null)
                {
                    return;
                }

                int count = 0;
                double total = 0D;
                using (Transaction transaction = document.Database.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId objectId in selectedIds)
                    {
                        DBObject value = transaction.GetObject(objectId, OpenMode.ForRead, false);
                        try
                        {
                            Curve curve = value as Curve;
                            Region region = value as Region;
                            Hatch hatch = value as Hatch;
                            if (curve != null && curve.Closed)
                            {
                                total += Math.Abs(curve.Area);
                                count++;
                            }
                            else if (region != null)
                            {
                                total += Math.Abs(region.Area);
                                count++;
                            }
                            else if (hatch != null)
                            {
                                total += Math.Abs(hatch.Area);
                                count++;
                            }
                        }
                        catch
                        {
                            // Invalid boundaries and unevaluated hatches are omitted.
                        }
                    }

                    transaction.Commit();
                }

                document.Editor.WriteMessage(
                    "\n面积统计：" + count + " 个有效对象，总面积 = "
                    + total.ToString("0.###", CultureInfo.CurrentCulture) + "。\n");
            });
        }

        [CommandMethod("DLTEXTSUM")]
        public void SumTextNumbers()
        {
            Run(delegate(Document document)
            {
                ObjectId[] selectedIds = GetSelection(document, "选择包含数字的文字", false);
                if (selectedIds == null)
                {
                    return;
                }

                int numberCount = 0;
                double total = 0D;
                using (Transaction transaction = document.Database.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId objectId in selectedIds)
                    {
                        DBObject value = transaction.GetObject(objectId, OpenMode.ForRead, false);
                        DBText dbText = value as DBText;
                        MText mText = value as MText;
                        string text = dbText != null ? dbText.TextString : (mText == null ? null : mText.Text);
                        if (string.IsNullOrEmpty(text))
                        {
                            continue;
                        }

                        foreach (Match match in NumberPattern.Matches(text))
                        {
                            double number;
                            if (double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
                            {
                                total += number;
                                numberCount++;
                            }
                        }
                    }

                    transaction.Commit();
                }

                document.Editor.WriteMessage(
                    "\n文字数字求和：识别 " + numberCount + " 个数值，合计 = "
                    + total.ToString("0.###", CultureInfo.CurrentCulture) + "。\n");
            });
        }

        private static void Run(Action<Document> action)
        {
            Document document = GcadApplication.DocumentManager.MdiActiveDocument;
            if (document == null)
            {
                return;
            }

            try
            {
                action(document);
            }
            catch (System.Exception ex)
            {
                document.Editor.WriteMessage("\n操作失败：" + ex.Message + "\n");
            }
        }

        private static void ModifySelectedText(
            Document document,
            string prompt,
            Action<DBText> dbTextAction,
            Action<MText> mTextAction)
        {
            ObjectId[] selectedIds = GetSelection(document, prompt, true);
            if (selectedIds == null)
            {
                return;
            }

            int changed = 0;
            int skipped = 0;
            using (Transaction transaction = document.Database.TransactionManager.StartTransaction())
            {
                foreach (ObjectId objectId in selectedIds)
                {
                    string dxfName = objectId.ObjectClass.DxfName;
                    if (!string.Equals(dxfName, "TEXT", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(dxfName, "MTEXT", StringComparison.OrdinalIgnoreCase))
                    {
                        skipped++;
                        continue;
                    }

                    DBObject value = transaction.GetObject(objectId, OpenMode.ForWrite, false);
                    DBText dbText = value as DBText;
                    MText mText = value as MText;
                    if (dbText != null && dbTextAction != null)
                    {
                        dbTextAction(dbText);
                        changed++;
                    }
                    else if (mText != null && mTextAction != null)
                    {
                        mTextAction(mText);
                        changed++;
                    }
                    else
                    {
                        skipped++;
                    }
                }

                transaction.Commit();
            }

            document.Editor.Regen();
            document.Editor.WriteMessage("\n已修改 " + changed + " 个文字，跳过 " + skipped + " 个对象。\n");
        }

        private static ObjectId[] GetSelection(Document document, string message, bool rejectLockedLayers)
        {
            PromptSelectionOptions options = new PromptSelectionOptions();
            options.MessageForAdding = "\n" + message + "：";
            options.MessageForRemoval = "\n移除对象：";
            options.RejectObjectsOnLockedLayers = rejectLockedLayers;
            options.RejectObjectsFromNonCurrentSpace = true;
            PromptSelectionResult result = document.Editor.GetSelection(options);
            return result.Status == PromptStatus.OK && result.Value != null
                ? result.Value.GetObjectIds()
                : null;
        }

        private static bool TryGetString(Editor editor, string prompt, bool allowEmpty, out string value)
        {
            PromptStringOptions options = new PromptStringOptions(prompt);
            options.AllowSpaces = true;

            PromptResult result = editor.GetString(options);
            if (result.Status == PromptStatus.OK || (allowEmpty && result.Status == PromptStatus.None))
            {
                value = result.StringResult ?? string.Empty;
                return allowEmpty || value.Length > 0;
            }

            value = null;
            return false;
        }

        private static bool TryGetPositiveDouble(Editor editor, string prompt, out double value)
        {
            PromptDoubleOptions options = new PromptDoubleOptions(prompt);
            options.AllowNegative = false;
            options.AllowZero = false;
            PromptDoubleResult result = editor.GetDouble(options);
            value = result.Status == PromptStatus.OK ? result.Value : 0D;
            return result.Status == PromptStatus.OK;
        }

        private static bool TryGetInteger(Editor editor, string prompt, int defaultValue, out int value)
        {
            PromptIntegerOptions options = new PromptIntegerOptions(prompt);
            options.AllowNone = true;
            options.AllowZero = false;
            options.DefaultValue = defaultValue;
            options.UseDefaultValue = true;
            PromptIntegerResult result = editor.GetInteger(options);
            value = result.Status == PromptStatus.OK ? result.Value : defaultValue;
            return result.Status == PromptStatus.OK || result.Status == PromptStatus.None;
        }

        private static bool TryGetPositiveInteger(
            Editor editor,
            string prompt,
            bool allowDefault,
            int defaultValue,
            out int value)
        {
            PromptIntegerOptions options = new PromptIntegerOptions(prompt);
            options.AllowNegative = false;
            options.AllowZero = false;
            options.LowerLimit = 1;
            options.AllowNone = allowDefault;
            if (allowDefault)
            {
                options.DefaultValue = defaultValue;
                options.UseDefaultValue = true;
            }

            PromptIntegerResult result = editor.GetInteger(options);
            value = result.Status == PromptStatus.OK ? result.Value : defaultValue;
            return result.Status == PromptStatus.OK || (allowDefault && result.Status == PromptStatus.None);
        }

        private static bool TrySwapSegments(
            string value,
            int firstPosition,
            int secondPosition,
            int length,
            out string result)
        {
            result = value;
            if (string.IsNullOrEmpty(value) || firstPosition < 1 || secondPosition < 1 || length < 1)
            {
                return false;
            }

            int[] elementStarts = StringInfo.ParseCombiningCharacters(value);
            int firstIndex = firstPosition - 1;
            int secondIndex = secondPosition - 1;
            if (firstIndex + length > elementStarts.Length || secondIndex + length > elementStarts.Length)
            {
                return false;
            }

            if (firstIndex < secondIndex + length && secondIndex < firstIndex + length)
            {
                return false;
            }

            string[] elements = new string[elementStarts.Length];
            for (int i = 0; i < elementStarts.Length; i++)
            {
                int end = i + 1 < elementStarts.Length ? elementStarts[i + 1] : value.Length;
                elements[i] = value.Substring(elementStarts[i], end - elementStarts[i]);
            }

            for (int offset = 0; offset < length; offset++)
            {
                string temporary = elements[firstIndex + offset];
                elements[firstIndex + offset] = elements[secondIndex + offset];
                elements[secondIndex + offset] = temporary;
            }

            result = string.Concat(elements);
            return true;
        }

        private static bool HasMTextFormatting(string value)
        {
            return !string.IsNullOrEmpty(value)
                && (value.IndexOf('\\') >= 0 || value.IndexOf('{') >= 0 || value.IndexOf('}') >= 0);
        }

        private static bool TryGetTextEntity(Document document, string prompt, out ObjectId objectId)
        {
            PromptEntityResult result = document.Editor.GetEntity(prompt);
            if (result.Status == PromptStatus.OK)
            {
                string dxfName = result.ObjectId.ObjectClass.DxfName;
                if (string.Equals(dxfName, "TEXT", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(dxfName, "MTEXT", StringComparison.OrdinalIgnoreCase))
                {
                    objectId = result.ObjectId;
                    return true;
                }

                document.Editor.WriteMessage("\n所选对象不是单行或多行文字。\n");
            }

            objectId = ObjectId.Null;
            return false;
        }

        private static string GetTextContent(Entity entity)
        {
            DBText dbText = entity as DBText;
            MText mText = entity as MText;
            return dbText != null ? dbText.TextString : (mText == null ? string.Empty : mText.Contents);
        }

        private static void SetTextContent(Entity entity, string value)
        {
            DBText dbText = entity as DBText;
            MText mText = entity as MText;
            if (dbText != null)
            {
                dbText.TextString = value;
            }
            else if (mText != null)
            {
                mText.Contents = value;
            }
        }

        private static Point3d GetTextAnchor(Entity entity)
        {
            DBText dbText = entity as DBText;
            if (dbText != null)
            {
                return dbText.IsDefaultAlignment ? dbText.Position : dbText.AlignmentPoint;
            }

            MText mText = entity as MText;
            return mText == null ? Point3d.Origin : mText.Location;
        }

        private static string GetEffectiveBlockName(BlockReference reference, Transaction transaction)
        {
            ObjectId definitionId = reference.IsDynamicBlock
                ? reference.DynamicBlockTableRecord
                : reference.BlockTableRecord;
            BlockTableRecord definition = transaction.GetObject(definitionId, OpenMode.ForRead) as BlockTableRecord;
            return definition == null ? "<未命名块>" : definition.Name;
        }

        private static int CompareTextTargets(TextTarget left, TextTarget right)
        {
            double rowTolerance = Math.Max(1D, Math.Max(Math.Abs(left.Position.Y), Math.Abs(right.Position.Y))) * 1E-8D;
            if (Math.Abs(left.Position.Y - right.Position.Y) > rowTolerance)
            {
                return right.Position.Y.CompareTo(left.Position.Y);
            }

            int xResult = left.Position.X.CompareTo(right.Position.X);
            return xResult != 0 ? xResult : left.ObjectId.Handle.Value.CompareTo(right.ObjectId.Handle.Value);
        }

        private sealed class TextTarget
        {
            public TextTarget(ObjectId objectId, Point3d position)
            {
                ObjectId = objectId;
                Position = position;
            }

            public ObjectId ObjectId { get; private set; }
            public Point3d Position { get; private set; }
        }
    }
}
