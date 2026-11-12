using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Gssoft.Gscad.ApplicationServices;
using Gssoft.Gscad.DatabaseServices;
using Gssoft.Gscad.EditorInput;
using Gssoft.Gscad.Geometry;
using Gssoft.Gscad.Runtime;
using GcadApplication = Gssoft.Gscad.ApplicationServices.Application;

namespace GstarCadDrawingLibrary
{
    public sealed class BlockEditingCommands
    {
        private static readonly Regex InvalidBlockNameCharacters = new Regex(
            "[<>/\\\\\":;?*|,=`]",
            RegexOptions.Compiled);

        [CommandMethod("DLBLOCKCREATE")]
        public void CreateBlock()
        {
            Run(delegate(Document document)
            {
                Editor editor = document.Editor;
                ObjectId[] selectedIds = GetSelection(document, "选择要创建为块的对象");
                if (selectedIds == null)
                {
                    return;
                }

                PromptPointResult pointResult = editor.GetPoint("\n指定块基点：");
                if (pointResult.Status != PromptStatus.OK)
                {
                    return;
                }

                string blockName;
                if (!TryGetBlockName(editor, "\n输入新块名称：", out blockName))
                {
                    return;
                }

                Database database = document.Database;
                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    BlockTable blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
                    if (blockTable.Has(blockName))
                    {
                        editor.WriteMessage("\n块名称已经存在。\n");
                        return;
                    }

                    blockTable.UpgradeOpen();
                    BlockTableRecord definition = new BlockTableRecord();
                    definition.Name = blockName;
                    definition.Origin = Point3d.Origin;
                    ObjectId definitionId = blockTable.Add(definition);
                    transaction.AddNewlyCreatedDBObject(definition, true);

                    Matrix3d toBlockCoordinates = Matrix3d.Displacement(
                        pointResult.Value.GetVectorTo(Point3d.Origin));
                    int entityCount = 0;
                    using (IdMapping mapping = new IdMapping())
                    {
                        database.DeepCloneObjects(
                            new ObjectIdCollection(selectedIds),
                            definitionId,
                            mapping,
                            false);
                        foreach (IdPair pair in mapping)
                        {
                            if (!pair.IsCloned || pair.Value.IsNull)
                            {
                                continue;
                            }

                            Entity clone = transaction.GetObject(pair.Value, OpenMode.ForWrite, false) as Entity;
                            if (clone != null)
                            {
                                clone.TransformBy(toBlockCoordinates);
                                if (pair.IsPrimary)
                                {
                                    entityCount++;
                                }
                            }
                        }
                    }

                    foreach (ObjectId objectId in selectedIds)
                    {
                        Entity original = transaction.GetObject(objectId, OpenMode.ForWrite, false) as Entity;
                        if (original != null)
                        {
                            original.Erase();
                        }
                    }

                    if (entityCount == 0)
                    {
                        definition.Erase();
                        editor.WriteMessage("\n所选内容中没有可创建块的对象。\n");
                        return;
                    }

                    BlockTableRecord currentSpace = (BlockTableRecord)transaction.GetObject(
                        database.CurrentSpaceId,
                        OpenMode.ForWrite);
                    BlockReference reference = new BlockReference(pointResult.Value, definitionId);
                    currentSpace.AppendEntity(reference);
                    transaction.AddNewlyCreatedDBObject(reference, true);
                    AppendAttributes(reference, definition, transaction, null);
                    transaction.Commit();

                    editor.SetImpliedSelection(new ObjectId[] { reference.ObjectId });
                    editor.WriteMessage("\n已创建块“" + blockName + "”，包含 " + entityCount + " 个对象。\n");
                }
            });
        }

        [CommandMethod("DLBLOCKRENAME")]
        public void RenameBlock()
        {
            Run(delegate(Document document)
            {
                ObjectId referenceId;
                if (!TryGetBlockReference(document, "\n选择要改名的块参照：", out referenceId))
                {
                    return;
                }

                string newName;
                if (!TryGetBlockName(document.Editor, "\n输入新的块名称：", out newName))
                {
                    return;
                }

                using (Transaction transaction = document.Database.TransactionManager.StartTransaction())
                {
                    BlockReference reference = (BlockReference)transaction.GetObject(referenceId, OpenMode.ForRead);
                    ObjectId definitionId = GetEffectiveDefinitionId(reference);
                    BlockTableRecord definition = (BlockTableRecord)transaction.GetObject(definitionId, OpenMode.ForWrite);
                    if (!CanEditDefinition(definition, document.Editor))
                    {
                        return;
                    }

                    BlockTable blockTable = (BlockTable)transaction.GetObject(document.Database.BlockTableId, OpenMode.ForRead);
                    if (blockTable.Has(newName)
                        && !string.Equals(definition.Name, newName, StringComparison.OrdinalIgnoreCase))
                    {
                        document.Editor.WriteMessage("\n目标块名称已经存在。\n");
                        return;
                    }

                    string oldName = definition.Name;
                    definition.Name = newName;
                    transaction.Commit();
                    document.Editor.WriteMessage("\n已将块“" + oldName + "”改名为“" + newName + "”。\n");
                }
            });
        }

        [CommandMethod("DLBLOCKREPLACE")]
        public void ReplaceBlocks()
        {
            Run(delegate(Document document)
            {
                ObjectId sourceReferenceId;
                if (!TryGetBlockReference(document, "\n选择要被替换的块参照：", out sourceReferenceId))
                {
                    return;
                }

                ObjectId targetReferenceId;
                if (!TryGetBlockReference(document, "\n选择作为替换样板的块参照：", out targetReferenceId))
                {
                    return;
                }

                int replaced = 0;
                Database database = document.Database;
                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    BlockReference sourceReference = (BlockReference)transaction.GetObject(sourceReferenceId, OpenMode.ForRead);
                    BlockReference targetReference = (BlockReference)transaction.GetObject(targetReferenceId, OpenMode.ForRead);
                    ObjectId sourceEffectiveId = GetEffectiveDefinitionId(sourceReference);
                    ObjectId targetEffectiveId = GetEffectiveDefinitionId(targetReference);
                    if (sourceEffectiveId == targetEffectiveId)
                    {
                        document.Editor.WriteMessage("\n两个块属于同一块定义，无需替换。\n");
                        return;
                    }

                    ObjectId targetDefinitionId = targetReference.BlockTableRecord;
                    BlockTableRecord targetDefinition = (BlockTableRecord)transaction.GetObject(
                        targetDefinitionId,
                        OpenMode.ForRead);
                    BlockTableRecord currentSpace = (BlockTableRecord)transaction.GetObject(
                        database.CurrentSpaceId,
                        OpenMode.ForRead);
                    List<ObjectId> currentSpaceIds = new List<ObjectId>();
                    foreach (ObjectId objectId in currentSpace)
                    {
                        currentSpaceIds.Add(objectId);
                    }

                    foreach (ObjectId objectId in currentSpaceIds)
                    {
                        BlockReference reference = transaction.GetObject(objectId, OpenMode.ForRead, false) as BlockReference;
                        if (reference == null || GetEffectiveDefinitionId(reference) != sourceEffectiveId)
                        {
                            continue;
                        }

                        reference.UpgradeOpen();
                        Dictionary<string, string> attributeValues = ReadAndEraseAttributes(reference, transaction);
                        reference.BlockTableRecord = targetDefinitionId;
                        AppendAttributes(reference, targetDefinition, transaction, attributeValues);
                        replaced++;
                    }

                    transaction.Commit();
                }

                document.Editor.Regen();
                document.Editor.WriteMessage("\n已替换当前空间中的 " + replaced + " 个块参照。\n");
            });
        }

        [CommandMethod("DLBLOCKSCALE")]
        public void ScaleBlocks()
        {
            Run(delegate(Document document)
            {
                double factor;
                if (!TryGetPositiveDouble(document.Editor, "\n输入块缩放倍数：", out factor))
                {
                    return;
                }

                ModifySelectedBlocks(document, "选择要缩放的块参照", delegate(BlockReference reference)
                {
                    Scale3d scale = reference.ScaleFactors;
                    reference.ScaleFactors = new Scale3d(scale.X * factor, scale.Y * factor, scale.Z * factor);
                });
            });
        }

        [CommandMethod("DLBLOCKROTATE")]
        public void RotateBlocks()
        {
            Run(delegate(Document document)
            {
                PromptDoubleResult result = document.Editor.GetAngle("\n指定块的增量旋转角度：");
                if (result.Status != PromptStatus.OK)
                {
                    return;
                }

                ModifySelectedBlocks(document, "选择要旋转的块参照", delegate(BlockReference reference)
                {
                    reference.Rotation += result.Value;
                });
            });
        }

        [CommandMethod("DLBLOCKZERO")]
        public void SetBlockEntitiesToLayerZero()
        {
            Run(delegate(Document document)
            {
                EditSelectedDefinition(document, "\n选择要将内部实体置于 0 层的块参照：", delegate(Entity entity)
                {
                    entity.Layer = "0";
                }, "已将块内 {0} 个实体置于 0 层。");
            });
        }

        [CommandMethod("DLBLOCKCOLOR")]
        public void SetBlockEntityColor()
        {
            Run(delegate(Document document)
            {
                PromptIntegerOptions options = new PromptIntegerOptions("\n输入颜色索引（0=随块，1-255=索引色，256=随层）：");
                options.LowerLimit = 0;
                options.UpperLimit = 256;
                options.AllowZero = true;
                PromptIntegerResult result = document.Editor.GetInteger(options);
                if (result.Status != PromptStatus.OK)
                {
                    return;
                }

                EditSelectedDefinition(document, "\n选择要修改内部实体颜色的块参照：", delegate(Entity entity)
                {
                    entity.ColorIndex = result.Value;
                }, "已修改块内 {0} 个实体的颜色。");
            });
        }

        [CommandMethod("DLBLOCKBRUSH")]
        public void BrushBlockProperties()
        {
            Run(delegate(Document document)
            {
                ObjectId sourceId;
                if (!TryGetBlockReference(document, "\n选择属性来源块参照：", out sourceId))
                {
                    return;
                }

                ObjectId[] targetIds = GetSelection(document, "选择要应用属性的目标块参照");
                if (targetIds == null)
                {
                    return;
                }

                int changed = 0;
                using (Transaction transaction = document.Database.TransactionManager.StartTransaction())
                {
                    BlockReference source = (BlockReference)transaction.GetObject(sourceId, OpenMode.ForRead);
                    Scale3d sourceScale = source.ScaleFactors;
                    double sourceRotation = source.Rotation;
                    ObjectId sourceLayerId = source.LayerId;
                    int sourceColorIndex = source.ColorIndex;
                    foreach (ObjectId objectId in targetIds)
                    {
                        BlockReference target = transaction.GetObject(objectId, OpenMode.ForWrite, false) as BlockReference;
                        if (target == null)
                        {
                            continue;
                        }

                        target.ScaleFactors = sourceScale;
                        target.Rotation = sourceRotation;
                        target.LayerId = sourceLayerId;
                        target.ColorIndex = sourceColorIndex;
                        changed++;
                    }

                    transaction.Commit();
                }

                document.Editor.Regen();
                document.Editor.WriteMessage("\n已向 " + changed + " 个块参照应用比例、角度、图层和颜色。\n");
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

        private static void ModifySelectedBlocks(Document document, string prompt, Action<BlockReference> action)
        {
            ObjectId[] selectedIds = GetSelection(document, prompt);
            if (selectedIds == null)
            {
                return;
            }

            int changed = 0;
            using (Transaction transaction = document.Database.TransactionManager.StartTransaction())
            {
                foreach (ObjectId objectId in selectedIds)
                {
                    if (!string.Equals(objectId.ObjectClass.DxfName, "INSERT", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    BlockReference reference = transaction.GetObject(objectId, OpenMode.ForWrite, false) as BlockReference;
                    if (reference != null)
                    {
                        action(reference);
                        changed++;
                    }
                }

                transaction.Commit();
            }

            document.Editor.Regen();
            document.Editor.WriteMessage("\n已修改 " + changed + " 个块参照。\n");
        }

        private static void EditSelectedDefinition(
            Document document,
            string prompt,
            Action<Entity> action,
            string resultMessage)
        {
            ObjectId referenceId;
            if (!TryGetBlockReference(document, prompt, out referenceId))
            {
                return;
            }

            int changed = 0;
            using (Transaction transaction = document.Database.TransactionManager.StartTransaction())
            {
                BlockReference reference = (BlockReference)transaction.GetObject(referenceId, OpenMode.ForRead);
                BlockTableRecord definition = (BlockTableRecord)transaction.GetObject(
                    GetEffectiveDefinitionId(reference),
                    OpenMode.ForRead);
                if (!CanEditDefinition(definition, document.Editor))
                {
                    return;
                }

                foreach (ObjectId objectId in definition)
                {
                    Entity entity = transaction.GetObject(objectId, OpenMode.ForWrite, false) as Entity;
                    if (entity != null)
                    {
                        action(entity);
                        changed++;
                    }
                }

                transaction.Commit();
            }

            document.Editor.Regen();
            document.Editor.WriteMessage("\n" + string.Format(resultMessage, changed) + "\n");
        }

        private static ObjectId[] GetSelection(Document document, string message)
        {
            PromptSelectionOptions options = new PromptSelectionOptions();
            options.MessageForAdding = "\n" + message + "：";
            options.MessageForRemoval = "\n移除对象：";
            options.RejectObjectsOnLockedLayers = true;
            options.RejectObjectsFromNonCurrentSpace = true;
            PromptSelectionResult result = document.Editor.GetSelection(options);
            return result.Status == PromptStatus.OK && result.Value != null
                ? result.Value.GetObjectIds()
                : null;
        }

        private static bool TryGetBlockReference(Document document, string prompt, out ObjectId objectId)
        {
            PromptEntityResult result = document.Editor.GetEntity(prompt);
            if (result.Status == PromptStatus.OK
                && string.Equals(result.ObjectId.ObjectClass.DxfName, "INSERT", StringComparison.OrdinalIgnoreCase))
            {
                objectId = result.ObjectId;
                return true;
            }

            if (result.Status == PromptStatus.OK)
            {
                document.Editor.WriteMessage("\n所选对象不是块参照。\n");
            }

            objectId = ObjectId.Null;
            return false;
        }

        private static bool TryGetBlockName(Editor editor, string prompt, out string name)
        {
            PromptStringOptions options = new PromptStringOptions(prompt);
            options.AllowSpaces = true;
            PromptResult result = editor.GetString(options);
            name = result.Status == PromptStatus.OK ? result.StringResult.Trim() : null;
            if (result.Status != PromptStatus.OK)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(name) || InvalidBlockNameCharacters.IsMatch(name))
            {
                editor.WriteMessage("\n块名称不能为空，且不能包含非法字符。\n");
                return false;
            }

            return true;
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

        private static ObjectId GetEffectiveDefinitionId(BlockReference reference)
        {
            return reference.IsDynamicBlock
                ? reference.DynamicBlockTableRecord
                : reference.BlockTableRecord;
        }

        private static bool CanEditDefinition(BlockTableRecord definition, Editor editor)
        {
            if (definition.IsAnonymous || definition.IsFromExternalReference || definition.IsLayout)
            {
                editor.WriteMessage("\n匿名块、外部参照块或布局块不能执行此操作。\n");
                return false;
            }

            return true;
        }

        private static Dictionary<string, string> ReadAndEraseAttributes(
            BlockReference reference,
            Transaction transaction)
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            List<ObjectId> attributeIds = new List<ObjectId>();
            foreach (ObjectId attributeId in reference.AttributeCollection)
            {
                attributeIds.Add(attributeId);
            }

            foreach (ObjectId attributeId in attributeIds)
            {
                AttributeReference attribute = transaction.GetObject(attributeId, OpenMode.ForWrite, false) as AttributeReference;
                if (attribute != null)
                {
                    values[attribute.Tag] = attribute.TextString;
                    attribute.Erase();
                }
            }

            return values;
        }

        private static void AppendAttributes(
            BlockReference reference,
            BlockTableRecord definition,
            Transaction transaction,
            IDictionary<string, string> previousValues)
        {
            foreach (ObjectId objectId in definition)
            {
                AttributeDefinition attributeDefinition = transaction.GetObject(
                    objectId,
                    OpenMode.ForRead,
                    false) as AttributeDefinition;
                if (attributeDefinition == null || attributeDefinition.Constant)
                {
                    continue;
                }

                AttributeReference attribute = new AttributeReference();
                attribute.SetAttributeFromBlock(attributeDefinition, reference.BlockTransform);
                string previousValue;
                if (previousValues != null && previousValues.TryGetValue(attributeDefinition.Tag, out previousValue))
                {
                    attribute.TextString = previousValue;
                }
                else
                {
                    attribute.TextString = attributeDefinition.TextString;
                }

                reference.AttributeCollection.AppendAttribute(attribute);
                transaction.AddNewlyCreatedDBObject(attribute, true);
            }
        }
    }
}
