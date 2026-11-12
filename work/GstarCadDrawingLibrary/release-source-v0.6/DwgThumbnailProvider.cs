using System;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Gssoft.Gscad.DatabaseServices;
using DrawingImage = System.Drawing.Image;

namespace GstarCadDrawingLibrary
{
    internal sealed class DwgThumbnailProvider
    {
        private readonly string cacheDirectory;

        public DwgThumbnailProvider()
        {
            cacheDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GstarCadDrawingLibrary",
                "ThumbnailCache");
        }

        public DrawingImage Load(DrawingItem item)
        {
            if (item == null)
            {
                return null;
            }

            DrawingImage sidecar = LoadImageFile(item.ThumbnailPath);
            if (sidecar != null)
            {
                return sidecar;
            }

            if (string.IsNullOrWhiteSpace(item.FilePath) || !File.Exists(item.FilePath))
            {
                return null;
            }

            try
            {
                string cachePath = GetCachePath(item.FilePath);
                DrawingImage cached = LoadImageFile(cachePath);
                if (cached != null)
                {
                    return cached;
                }

                using (Database database = new Database(false, true))
                {
                    database.RetainOriginalThumbnailBitmap = true;
                    database.ReadDwgFile(
                        item.FilePath,
                        FileOpenMode.OpenForReadAndAllShare,
                        true,
                        string.Empty);

                    using (Bitmap embedded = database.ThumbnailBitmap)
                    {
                        if (embedded == null || embedded.Width <= 1 || embedded.Height <= 1)
                        {
                            return null;
                        }

                        Bitmap result = new Bitmap(embedded);
                        TrySaveCache(result, cachePath);
                        return result;
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        private static DrawingImage LoadImageFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            try
            {
                using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (DrawingImage original = DrawingImage.FromStream(stream))
                {
                    return new Bitmap(original);
                }
            }
            catch
            {
                return null;
            }
        }

        private string GetCachePath(string drawingPath)
        {
            FileInfo info = new FileInfo(drawingPath);
            string identity = Path.GetFullPath(drawingPath).ToUpperInvariant()
                + "|" + info.Length
                + "|" + info.LastWriteTimeUtc.Ticks;
            byte[] data = Encoding.UTF8.GetBytes(identity);
            string hash;
            using (SHA1 algorithm = SHA1.Create())
            {
                hash = BitConverter.ToString(algorithm.ComputeHash(data)).Replace("-", string.Empty);
            }

            return Path.Combine(cacheDirectory, hash + ".png");
        }

        private void TrySaveCache(Bitmap bitmap, string path)
        {
            try
            {
                Directory.CreateDirectory(cacheDirectory);
                bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
            catch
            {
                // The preview remains usable even when the cache directory is read-only.
            }
        }
    }
}
