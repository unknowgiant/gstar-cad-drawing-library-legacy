using System;
using System.IO;
using System.Text.RegularExpressions;
using Gssoft.Gscad.ApplicationServices;
using Gssoft.Gscad.DatabaseServices;
using Gssoft.Gscad.EditorInput;
using Gssoft.Gscad.Geometry;

namespace GstarCadDrawingLibrary
{
    internal static class DwgInsertService
    {
        private static readonly Regex InvalidBlockNameCharacters = new Regex("[<>/\\\\\":;?*|,=`]", RegexOptions.Compiled);

        public static void Insert(Document document, InsertDrawingEventArgs request)
        {
            Editor editor = document.Editor;
            if (request == null || string.IsNullOrWhiteSpace(request.FilePath) || !File.Exists(request.FilePath))
            {
                editor.WriteMessage("\n图纸文件不存在。\n");
                return;
            }

            if (string.Equals(Path.GetFullPath(request.FilePath), Path.GetFullPath(document.Name), StringComparison.OrdinalIgnoreCase))
            {
                editor.WriteMessage("\n不能将当前图纸作为块插入自身。\n");
                return;
            }

            PromptPointResult pointResult = editor.GetPoint("\n指定插入点：");
            if (pointResult.Status != PromptStatus.OK)
            {
                return;
            }

            InsertAt(document, request, pointResult.Value);
        }

        internal static void InsertAt(Document document, InsertDrawingEventArgs request, Point3d insertionPoint)
        {
            Editor editor = document.Editor;
            try
            {
                Database targetDatabase = document.Database;
                string blockName = MakeBlockName(request.FilePath);
                ObjectId blockDefinitionId;
                bool blockExists;

                using (Transaction lookupTransaction = targetDatabase.TransactionManager.StartTransaction())
                {
                    BlockTable blockTable = (BlockTable)lookupTransaction.GetObject(targetDatabase.BlockTableId, OpenMode.ForRead);
                    blockExists = blockTable.Has(blockName);
                    blockDefinitionId = blockExists ? blockTable[blockName] : ObjectId.Null;
                    lookupTransaction.Commit();
                }

                if (!blockExists)
                {
                    using (Database sourceDatabase = new Database(false, true))
                    {
                        sourceDatabase.ReadDwgFile(
                            request.FilePath,
                            FileOpenMode.OpenForReadAndAllShare,
                            true,
                            string.Empty);
                        blockDefinitionId = targetDatabase.Insert(blockName, sourceDatabase, false);
                    }
                }

                using (Transaction transaction = targetDatabase.TransactionManager.StartTransaction())
                {
                    BlockTableRecord currentSpace = (BlockTableRecord)transaction.GetObject(
                        targetDatabase.CurrentSpaceId,
                        OpenMode.ForWrite);

                    using (BlockReference reference = new BlockReference(insertionPoint, blockDefinitionId))
                    {
                        reference.ScaleFactors = new Scale3d(request.Scale);
                        reference.Rotation = request.RotationDegrees * Math.PI / 180D;
                        reference.LayerId = targetDatabase.Clayer;
                        currentSpace.AppendEntity(reference);
                        transaction.AddNewlyCreatedDBObject(reference, true);
                    }

                    transaction.Commit();
                }

                editor.WriteMessage("\n已插入图纸：" + Path.GetFileName(request.FilePath) + "\n");
            }
            catch (Exception ex)
            {
                editor.WriteMessage("\n插入失败：" + ex.Message + "\n");
            }
        }

        private static string MakeBlockName(string filePath)
        {
            string name = Path.GetFileNameWithoutExtension(filePath).Trim();
            name = InvalidBlockNameCharacters.Replace(name, "_");
            return string.IsNullOrWhiteSpace(name) ? "DrawingLibraryBlock" : name;
        }
    }
}
