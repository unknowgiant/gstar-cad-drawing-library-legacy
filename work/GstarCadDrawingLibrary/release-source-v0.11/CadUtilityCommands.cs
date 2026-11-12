using System;
using System.Collections.Generic;
using Gssoft.Gscad.ApplicationServices;
using Gssoft.Gscad.DatabaseServices;
using Gssoft.Gscad.EditorInput;
using Gssoft.Gscad.Runtime;
using GcadApplication = Gssoft.Gscad.ApplicationServices.Application;

namespace GstarCadDrawingLibrary
{
    public sealed class CadUtilityCommands
    {
        private static readonly Dictionary<Database, Dictionary<string, bool>> IsolationSnapshots =
            new Dictionary<Database, Dictionary<string, bool>>();

        [CommandMethod("DLSELLAYER")]
        public void SelectSameLayer()
        {
            Run(delegate(Document document)
            {
                SelectMatching(document, "\n选择一个对象，选取其所在图层的全部对象：", delegate(Entity selected, Entity candidate, Transaction transaction)
                {
                    return string.Equals(selected.Layer, candidate.Layer, StringComparison.OrdinalIgnoreCase);
                });
            });
        }

        [CommandMethod("DLSELBLOCK")]
        public void SelectSameBlock()
        {
            Run(delegate(Document document)
            {
                SelectMatching(document, "\n选择一个块参照，选取全部同名块：", delegate(Entity selected, Entity candidate, Transaction transaction)
                {
                    BlockReference selectedBlock = selected as BlockReference;
                    BlockReference candidateBlock = candidate as BlockReference;
                    if (selectedBlock == null || candidateBlock == null)
                    {
                        return false;
                    }

                    return string.Equals(
                        GetEffectiveBlockName(selectedBlock, transaction),
                        GetEffectiveBlockName(candidateBlock, transaction),
                        StringComparison.OrdinalIgnoreCase);
                }, true);
            });
        }

        [CommandMethod("DLSELCOLOR")]
        public void SelectSameColor()
        {
            Run(delegate(Document document)
            {
                SelectMatching(document, "\n选择一个对象，选取颜色属性相同的全部对象：", delegate(Entity selected, Entity candidate, Transaction transaction)
                {
                    return selected.ColorIndex == candidate.ColorIndex;
                });
            });
        }

        [CommandMethod("DLSELTYPE")]
        public void SelectSameEntityType()
        {
            Run(delegate(Document document)
            {
                SelectMatching(document, "\n选择一个对象，选取全部同类型对象：", delegate(Entity selected, Entity candidate, Transaction transaction)
                {
                    return string.Equals(
                        selected.ObjectId.ObjectClass.DxfName,
                        candidate.ObjectId.ObjectClass.DxfName,
                        StringComparison.OrdinalIgnoreCase);
                });
            });
        }

        [CommandMethod("DLLAYERISOLATE")]
        public void IsolateLayer()
        {
            Run(delegate(Document document)
            {
                string layerName;
                if (!TryGetSelectedLayer(document, "\n选择要隔离图层上的对象：", out layerName))
                {
                    return;
                }

                Database database = document.Database;
                Dictionary<string, bool> snapshot = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    LayerTable layerTable = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
                    Dictionary<string, bool> previous;
                    if (IsolationSnapshots.TryGetValue(database, out previous))
                    {
                        RestoreLayerStates(layerTable, transaction, previous);
                    }

                    foreach (ObjectId layerId in layerTable)
                    {
                        LayerTableRecord layer = (LayerTableRecord)transaction.GetObject(layerId, OpenMode.ForWrite);
                        snapshot[layer.Name] = layer.IsOff;
                        layer.IsOff = !string.Equals(layer.Name, layerName, StringComparison.OrdinalIgnoreCase);
                    }

                    transaction.Commit();
                }

                IsolationSnapshots[database] = snapshot;
                document.Editor.Regen();
                document.Editor.WriteMessage("\n已隔离图层：" + layerName + "。输入 DLLAYERRESTORE 恢复。\n");
            });
        }

        [CommandMethod("DLLAYERRESTORE")]
        public void RestoreLayers()
        {
            Run(delegate(Document document)
            {
                Database database = document.Database;
                Dictionary<string, bool> snapshot;
                if (!IsolationSnapshots.TryGetValue(database, out snapshot))
                {
                    document.Editor.WriteMessage("\n当前图纸没有可恢复的图层隔离记录。\n");
                    return;
                }

                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    LayerTable layerTable = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
                    RestoreLayerStates(layerTable, transaction, snapshot);
                    transaction.Commit();
                }

                IsolationSnapshots.Remove(database);
                document.Editor.Regen();
                document.Editor.WriteMessage("\n已恢复隔离前的图层显示状态。\n");
            });
        }

        [CommandMethod("DLLAYERFREEZE")]
        public void FreezeLayer()
        {
            Run(delegate(Document document)
            {
                string layerName;
                if (!TryGetSelectedLayer(document, "\n选择要冻结图层上的对象：", out layerName))
                {
                    return;
                }

                Database database = document.Database;
                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    LayerTable layerTable = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
                    ObjectId layerId = layerTable[layerName];
                    if (layerId == database.Clayer)
                    {
                        document.Editor.WriteMessage("\n不能冻结当前图层，请先切换当前图层。\n");
                        return;
                    }

                    LayerTableRecord layer = (LayerTableRecord)transaction.GetObject(layerId, OpenMode.ForWrite);
                    layer.IsFrozen = true;
                    transaction.Commit();
                }

                document.Editor.Regen();
                document.Editor.WriteMessage("\n已冻结图层：" + layerName + "。\n");
            });
        }

        [CommandMethod("DLLAYERLOCK")]
        public void LockLayer()
        {
            Run(delegate(Document document)
            {
                string layerName;
                if (!TryGetSelectedLayer(document, "\n选择要锁定图层上的对象：", out layerName))
                {
                    return;
                }

                using (Transaction transaction = document.Database.TransactionManager.StartTransaction())
                {
                    LayerTable layerTable = (LayerTable)transaction.GetObject(document.Database.LayerTableId, OpenMode.ForRead);
                    LayerTableRecord layer = (LayerTableRecord)transaction.GetObject(layerTable[layerName], OpenMode.ForWrite);
                    layer.IsLocked = true;
                    transaction.Commit();
                }

                document.Editor.Regen();
                document.Editor.WriteMessage("\n已锁定图层：" + layerName + "。\n");
            });
        }

        [CommandMethod("DLLAYERUNLOCKALL")]
        public void UnlockAllLayers()
        {
            Run(delegate(Document document)
            {
                int count = 0;
                Database database = document.Database;
                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    LayerTable layerTable = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
                    foreach (ObjectId layerId in layerTable)
                    {
                        LayerTableRecord layer = (LayerTableRecord)transaction.GetObject(layerId, OpenMode.ForRead);
                        if (!layer.IsDependent && layer.IsLocked)
                        {
                            layer.UpgradeOpen();
                            layer.IsLocked = false;
                            count++;
                        }
                    }

                    transaction.Commit();
                }

                document.Editor.Regen();
                document.Editor.WriteMessage("\n已解锁 " + count + " 个图层。\n");
            });
        }

        [CommandMethod("DLLAYEROFF")]
        public void TurnSelectedLayerOff()
        {
            Run(delegate(Document document)
            {
                string layerName;
                if (!TryGetSelectedLayer(document, "\n选择要关闭图层上的对象：", out layerName))
                {
                    return;
                }

                Database database = document.Database;
                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    LayerTable layerTable = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
                    ObjectId layerId = layerTable[layerName];
                    if (layerId == database.Clayer)
                    {
                        document.Editor.WriteMessage("\n不能关闭当前图层，请先切换当前图层。\n");
                        return;
                    }

                    LayerTableRecord layer = (LayerTableRecord)transaction.GetObject(layerId, OpenMode.ForWrite);
                    layer.IsOff = true;
                    transaction.Commit();
                }

                document.Editor.Regen();
                document.Editor.WriteMessage("\n已关闭图层：" + layerName + "。\n");
            });
        }

        [CommandMethod("DLLAYERSETCURRENT")]
        public void SetSelectedLayerCurrent()
        {
            Run(delegate(Document document)
            {
                string layerName;
                if (!TryGetSelectedLayer(document, "\n选择要设为当前图层的对象：", out layerName))
                {
                    return;
                }

                Database database = document.Database;
                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    LayerTable layerTable = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
                    ObjectId layerId = layerTable[layerName];
                    LayerTableRecord layer = (LayerTableRecord)transaction.GetObject(layerId, OpenMode.ForWrite);
                    if (layer.IsDependent)
                    {
                        document.Editor.WriteMessage("\n外部参照依赖图层不能设为当前图层。\n");
                        return;
                    }

                    layer.IsOff = false;
                    layer.IsFrozen = false;
                    database.Clayer = layerId;
                    transaction.Commit();
                }

                document.Editor.Regen();
                document.Editor.WriteMessage("\n当前图层已设为：" + layerName + "。\n");
            });
        }

        [CommandMethod("DLLAYERMOVE")]
        public void MoveObjectsToSampleLayer()
        {
            Run(delegate(Document document)
            {
                string layerName;
                if (!TryGetSelectedLayer(document, "\n选择目标图层上的样板对象：", out layerName))
                {
                    return;
                }

                ObjectId[] selectedIds = GetSelection(document, "选择要移动到目标图层的对象");
                if (selectedIds == null)
                {
                    return;
                }

                int changed = 0;
                Database database = document.Database;
                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    LayerTable layerTable = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
                    ObjectId targetLayerId = layerTable[layerName];
                    LayerTableRecord targetLayer = (LayerTableRecord)transaction.GetObject(targetLayerId, OpenMode.ForRead);
                    if (targetLayer.IsDependent)
                    {
                        document.Editor.WriteMessage("\n不能把对象移动到外部参照依赖图层。\n");
                        return;
                    }

                    foreach (ObjectId objectId in selectedIds)
                    {
                        Entity entity = transaction.GetObject(objectId, OpenMode.ForWrite, false) as Entity;
                        if (entity != null)
                        {
                            entity.LayerId = targetLayerId;
                            changed++;
                        }
                    }

                    transaction.Commit();
                }

                document.Editor.Regen();
                document.Editor.WriteMessage("\n已将 " + changed + " 个对象移动到图层“" + layerName + "”。\n");
            });
        }

        [CommandMethod("DLLAYERSWAP")]
        public void SwapCurrentSpaceLayerEntities()
        {
            Run(delegate(Document document)
            {
                string firstLayerName;
                if (!TryGetSelectedLayer(document, "\n选择第一个图层上的对象：", out firstLayerName))
                {
                    return;
                }

                string secondLayerName;
                if (!TryGetSelectedLayer(document, "\n选择第二个图层上的对象：", out secondLayerName))
                {
                    return;
                }

                if (string.Equals(firstLayerName, secondLayerName, StringComparison.OrdinalIgnoreCase))
                {
                    document.Editor.WriteMessage("\n两个对象位于同一图层，无需互换。\n");
                    return;
                }

                int firstToSecond = 0;
                int secondToFirst = 0;
                Database database = document.Database;
                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    LayerTable layerTable = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
                    ObjectId firstLayerId = layerTable[firstLayerName];
                    ObjectId secondLayerId = layerTable[secondLayerName];
                    LayerTableRecord firstLayer = (LayerTableRecord)transaction.GetObject(firstLayerId, OpenMode.ForWrite);
                    LayerTableRecord secondLayer = (LayerTableRecord)transaction.GetObject(secondLayerId, OpenMode.ForWrite);
                    if (firstLayer.IsDependent || secondLayer.IsDependent)
                    {
                        document.Editor.WriteMessage("\n外部参照依赖图层不能参与实体互换。\n");
                        return;
                    }

                    bool firstWasLocked = firstLayer.IsLocked;
                    bool secondWasLocked = secondLayer.IsLocked;
                    firstLayer.IsLocked = false;
                    secondLayer.IsLocked = false;

                    BlockTableRecord currentSpace = (BlockTableRecord)transaction.GetObject(
                        database.CurrentSpaceId,
                        OpenMode.ForRead);
                    List<ObjectId> entityIds = new List<ObjectId>();
                    foreach (ObjectId objectId in currentSpace)
                    {
                        entityIds.Add(objectId);
                    }

                    foreach (ObjectId objectId in entityIds)
                    {
                        Entity entity = transaction.GetObject(objectId, OpenMode.ForRead, false) as Entity;
                        if (entity == null)
                        {
                            continue;
                        }

                        ObjectId originalLayerId = entity.LayerId;
                        if (originalLayerId != firstLayerId && originalLayerId != secondLayerId)
                        {
                            continue;
                        }

                        entity.UpgradeOpen();
                        if (originalLayerId == firstLayerId)
                        {
                            entity.LayerId = secondLayerId;
                            firstToSecond++;
                        }
                        else
                        {
                            entity.LayerId = firstLayerId;
                            secondToFirst++;
                        }
                    }

                    firstLayer.IsLocked = firstWasLocked;
                    secondLayer.IsLocked = secondWasLocked;
                    transaction.Commit();
                }

                document.Editor.Regen();
                document.Editor.WriteMessage(
                    "\n当前空间图层实体互换完成：“" + firstLayerName + "”转出 " + firstToSecond
                    + "，“" + secondLayerName + "”转出 " + secondToFirst + "。\n");
            });
        }

        [CommandMethod("DLLAYERONALL")]
        public void TurnAllLayersOn()
        {
            Run(delegate(Document document)
            {
                int changed = ChangeAllLayers(document, delegate(LayerTableRecord layer)
                {
                    if (layer.IsOff)
                    {
                        layer.IsOff = false;
                        return true;
                    }

                    return false;
                });
                document.Editor.WriteMessage("\n已打开 " + changed + " 个图层。\n");
            });
        }

        [CommandMethod("DLLAYERTHAWALL")]
        public void ThawAllLayers()
        {
            Run(delegate(Document document)
            {
                int changed = ChangeAllLayers(document, delegate(LayerTableRecord layer)
                {
                    if (layer.IsFrozen)
                    {
                        layer.IsFrozen = false;
                        return true;
                    }

                    return false;
                });
                document.Editor.WriteMessage("\n已解冻 " + changed + " 个图层。\n");
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

        private static ObjectId[] GetSelection(Document document, string message)
        {
            PromptSelectionOptions options = new PromptSelectionOptions();
            options.MessageForAdding = "\n" + message + "：";
            options.MessageForRemoval = "\n移除对象：";
            options.RejectObjectsFromNonCurrentSpace = true;
            options.RejectObjectsOnLockedLayers = true;
            PromptSelectionResult result = document.Editor.GetSelection(options);
            return result.Status == PromptStatus.OK && result.Value != null
                ? result.Value.GetObjectIds()
                : null;
        }

        private static int ChangeAllLayers(Document document, Func<LayerTableRecord, bool> action)
        {
            int changed = 0;
            Database database = document.Database;
            using (Transaction transaction = database.TransactionManager.StartTransaction())
            {
                LayerTable layerTable = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
                foreach (ObjectId layerId in layerTable)
                {
                    LayerTableRecord layer = (LayerTableRecord)transaction.GetObject(layerId, OpenMode.ForRead);
                    if (layer.IsDependent)
                    {
                        continue;
                    }

                    layer.UpgradeOpen();
                    if (action(layer))
                    {
                        changed++;
                    }
                }

                transaction.Commit();
            }

            document.Editor.Regen();
            return changed;
        }

        private static void SelectMatching(
            Document document,
            string prompt,
            Func<Entity, Entity, Transaction, bool> predicate,
            bool requireBlockReference = false)
        {
            Editor editor = document.Editor;
            PromptEntityResult selectedResult = editor.GetEntity(prompt);
            if (selectedResult.Status != PromptStatus.OK)
            {
                return;
            }

            PromptSelectionResult allResult = editor.SelectAll();
            if (allResult.Status != PromptStatus.OK || allResult.Value == null)
            {
                editor.WriteMessage("\n当前空间没有可选择的对象。\n");
                return;
            }

            List<ObjectId> matches = new List<ObjectId>();
            using (Transaction transaction = document.Database.TransactionManager.StartTransaction())
            {
                Entity selected = transaction.GetObject(selectedResult.ObjectId, OpenMode.ForRead, false) as Entity;
                if (selected == null || (requireBlockReference && !(selected is BlockReference)))
                {
                    editor.WriteMessage(requireBlockReference ? "\n所选对象不是块参照。\n" : "\n所选对象无效。\n");
                    return;
                }

                foreach (ObjectId objectId in allResult.Value.GetObjectIds())
                {
                    if (!objectId.IsValid || objectId.IsErased)
                    {
                        continue;
                    }

                    Entity candidate = transaction.GetObject(objectId, OpenMode.ForRead, false) as Entity;
                    if (candidate != null && predicate(selected, candidate, transaction))
                    {
                        matches.Add(objectId);
                    }
                }

                transaction.Commit();
            }

            editor.SetImpliedSelection(matches.ToArray());
            editor.WriteMessage("\n已选择 " + matches.Count + " 个对象。\n");
        }

        private static string GetEffectiveBlockName(BlockReference reference, Transaction transaction)
        {
            ObjectId definitionId = reference.IsDynamicBlock
                ? reference.DynamicBlockTableRecord
                : reference.BlockTableRecord;
            BlockTableRecord definition = transaction.GetObject(definitionId, OpenMode.ForRead) as BlockTableRecord;
            return definition == null ? string.Empty : definition.Name;
        }

        private static bool TryGetSelectedLayer(Document document, string prompt, out string layerName)
        {
            PromptEntityResult result = document.Editor.GetEntity(prompt);
            if (result.Status != PromptStatus.OK)
            {
                layerName = null;
                return false;
            }

            using (Transaction transaction = document.Database.TransactionManager.StartTransaction())
            {
                Entity entity = transaction.GetObject(result.ObjectId, OpenMode.ForRead, false) as Entity;
                layerName = entity == null ? null : entity.Layer;
                transaction.Commit();
            }

            return !string.IsNullOrEmpty(layerName);
        }

        private static void RestoreLayerStates(
            LayerTable layerTable,
            Transaction transaction,
            Dictionary<string, bool> states)
        {
            foreach (KeyValuePair<string, bool> state in states)
            {
                if (!layerTable.Has(state.Key))
                {
                    continue;
                }

                LayerTableRecord layer = (LayerTableRecord)transaction.GetObject(layerTable[state.Key], OpenMode.ForWrite);
                layer.IsOff = state.Value;
            }
        }
    }
}
