using System;
using System.Collections.Generic;
using System.IO;

namespace GstarCadDrawingLibrary
{
    internal sealed class DrawingLibraryService
    {
        private static readonly string[] ThumbnailExtensions = { ".png", ".jpg", ".jpeg", ".bmp" };

        public IList<DrawingItem> Scan(string rootDirectory)
        {
            List<DrawingItem> items = new List<DrawingItem>();
            if (string.IsNullOrWhiteSpace(rootDirectory) || !Directory.Exists(rootDirectory))
            {
                return items;
            }

            string root = EnsureTrailingSeparator(Path.GetFullPath(rootDirectory));
            foreach (string filePath in Directory.EnumerateFiles(rootDirectory, "*.dwg", SearchOption.AllDirectories))
            {
                string directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
                string category = string.Empty;
                if (directory.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                {
                    category = directory.Substring(root.Length);
                }

                DrawingItem item = new DrawingItem();
                item.Name = Path.GetFileNameWithoutExtension(filePath);
                item.Category = category;
                item.FilePath = filePath;
                item.ThumbnailPath = FindThumbnail(filePath);
                items.Add(item);
            }

            items.Sort(delegate(DrawingItem left, DrawingItem right)
            {
                int categoryResult = string.Compare(left.Category, right.Category, StringComparison.CurrentCultureIgnoreCase);
                return categoryResult != 0
                    ? categoryResult
                    : string.Compare(left.Name, right.Name, StringComparison.CurrentCultureIgnoreCase);
            });

            return items;
        }

        public IList<string> GetCategories(string rootDirectory)
        {
            List<string> categories = new List<string>();
            if (!Directory.Exists(rootDirectory))
            {
                return categories;
            }

            string root = EnsureTrailingSeparator(Path.GetFullPath(rootDirectory));
            foreach (string directory in Directory.EnumerateDirectories(rootDirectory, "*", SearchOption.AllDirectories))
            {
                string fullPath = Path.GetFullPath(directory);
                if (fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                {
                    categories.Add(fullPath.Substring(root.Length));
                }
            }

            categories.Sort(StringComparer.CurrentCultureIgnoreCase);
            return categories;
        }

        private static string FindThumbnail(string drawingPath)
        {
            string basePath = Path.Combine(Path.GetDirectoryName(drawingPath), Path.GetFileNameWithoutExtension(drawingPath));
            foreach (string extension in ThumbnailExtensions)
            {
                string candidate = basePath + extension;
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static string EnsureTrailingSeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? path
                : path + Path.DirectorySeparatorChar;
        }
    }
}
