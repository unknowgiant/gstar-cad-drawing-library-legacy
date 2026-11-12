using System;
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

            Directory.CreateDirectory(settings.RootDirectory);
            return settings;
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
    }
}
