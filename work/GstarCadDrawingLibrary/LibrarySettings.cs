using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace GstarCadDrawingLibrary
{
    [DataContract]
    internal sealed class LibrarySettings
    {
        private const string SettingsFolderName = "GstarCadDrawingLibrary";

        [DataMember(Name = "rootDirectory")]
        public string RootDirectory { get; set; }

        [DataMember(Name = "recentFiles")]
        public List<string> RecentFiles { get; set; }

        public static LibrarySettings Load()
        {
            LibrarySettings settings = null;
            string path = GetSettingsPath();

            if (File.Exists(path))
            {
                try
                {
                    using (FileStream stream = File.OpenRead(path))
                    {
                        DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(LibrarySettings));
                        settings = serializer.ReadObject(stream) as LibrarySettings;
                    }
                }
                catch
                {
                    settings = null;
                }
            }

            if (settings == null || string.IsNullOrWhiteSpace(settings.RootDirectory))
            {
                settings = new LibrarySettings();
                settings.RootDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "GstarCAD 图纸库");
            }

            if (settings.RecentFiles == null)
            {
                settings.RecentFiles = new List<string>();
            }

            settings.RemoveMissingRecentFiles();

            Directory.CreateDirectory(settings.RootDirectory);
            return settings;
        }

        public void AddRecentFile(string filePath)
        {
            AddRecentFiles(new string[] { filePath });
        }

        public void AddRecentFiles(IEnumerable<string> filePaths)
        {
            if (filePaths == null)
            {
                return;
            }

            if (RecentFiles == null)
            {
                RecentFiles = new List<string>();
            }

            bool changed = false;
            foreach (string filePath in filePaths)
            {
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    continue;
                }

                for (int i = RecentFiles.Count - 1; i >= 0; i--)
                {
                    if (string.Equals(RecentFiles[i], filePath, StringComparison.OrdinalIgnoreCase))
                    {
                        RecentFiles.RemoveAt(i);
                    }
                }

                RecentFiles.Insert(0, filePath);
                changed = true;
            }

            if (RecentFiles.Count > 20)
            {
                RecentFiles.RemoveRange(20, RecentFiles.Count - 20);
            }

            if (changed)
            {
                Save();
            }
        }

        public void ClearRecentFiles()
        {
            if (RecentFiles == null || RecentFiles.Count == 0)
            {
                return;
            }

            RecentFiles.Clear();
            Save();
        }

        public void ReplaceRecentFile(string oldPath, string newPath)
        {
            bool changed = false;
            for (int i = 0; i < RecentFiles.Count; i++)
            {
                if (string.Equals(RecentFiles[i], oldPath, StringComparison.OrdinalIgnoreCase))
                {
                    RecentFiles[i] = newPath;
                    changed = true;
                }
            }

            if (changed)
            {
                Save();
            }
        }

        public void ReplaceRecentDirectory(string oldDirectory, string newDirectory)
        {
            string oldPrefix = EnsureTrailingSeparator(Path.GetFullPath(oldDirectory));
            bool changed = false;
            for (int i = 0; i < RecentFiles.Count; i++)
            {
                string fullPath = Path.GetFullPath(RecentFiles[i]);
                if (fullPath.StartsWith(oldPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    RecentFiles[i] = Path.Combine(newDirectory, fullPath.Substring(oldPrefix.Length));
                    changed = true;
                }
            }

            if (changed)
            {
                Save();
            }
        }

        public void RemoveRecentFile(string filePath)
        {
            bool changed = false;
            for (int i = RecentFiles.Count - 1; i >= 0; i--)
            {
                if (string.Equals(RecentFiles[i], filePath, StringComparison.OrdinalIgnoreCase))
                {
                    RecentFiles.RemoveAt(i);
                    changed = true;
                }
            }

            if (changed)
            {
                Save();
            }
        }

        public void RemoveRecentDirectory(string directory)
        {
            string prefix = EnsureTrailingSeparator(Path.GetFullPath(directory));
            bool changed = false;
            for (int i = RecentFiles.Count - 1; i >= 0; i--)
            {
                if (Path.GetFullPath(RecentFiles[i]).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    RecentFiles.RemoveAt(i);
                    changed = true;
                }
            }

            if (changed)
            {
                Save();
            }
        }

        public void Save()
        {
            string path = GetSettingsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            using (FileStream stream = File.Create(path))
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(LibrarySettings));
                serializer.WriteObject(stream, this);
            }
        }

        private static string GetSettingsPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                SettingsFolderName,
                "settings.json");
        }

        private void RemoveMissingRecentFiles()
        {
            for (int i = RecentFiles.Count - 1; i >= 0; i--)
            {
                if (string.IsNullOrWhiteSpace(RecentFiles[i]) || !File.Exists(RecentFiles[i]))
                {
                    RecentFiles.RemoveAt(i);
                }
            }
        }

        private static string EnsureTrailingSeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? path
                : path + Path.DirectorySeparatorChar;
        }
    }
}
