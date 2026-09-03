using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace OsuPatcher.Shared
{
    internal sealed class ConfigStore
    {
        private const string DirectoryName = "osu! patcher";
        private const string FileName = "config.ini";
        private readonly Dictionary<string, string> _values =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _keys = new List<string>();

        private ConfigStore(string filePath)
        {
            FilePath = filePath;
        }

        internal string FilePath { get; }

        internal static string DefaultPath
        {
            get
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return string.IsNullOrWhiteSpace(localAppData)
                    ? Path.GetFullPath(FileName)
                    : Path.Combine(localAppData, DirectoryName, FileName);
            }
        }

        internal static ConfigStore Load(string filePath = null)
        {
            string targetPath = filePath ?? DefaultPath;
            var store = new ConfigStore(targetPath);
            if (!File.Exists(targetPath))
                return store;

            foreach (string line in File.ReadAllLines(targetPath))
            {
                int separator = line.IndexOf('=');
                if (separator <= 0)
                    continue;

                string key = line.Substring(0, separator).Trim();
                string value = line.Substring(separator + 1).Trim();
                if (key.Length == 0)
                    continue;

                if (!store._values.ContainsKey(key))
                    store._keys.Add(key);

                store._values[key] = value;
            }

            return store;
        }

        internal static void Update(string filePath, string key, object value)
        {
            ConfigStore store = Load(filePath);
            store.Set(key, value);
            store.Save();
        }

        internal string GetString(string key, string defaultValue = null)
        {
            return _values.TryGetValue(key, out string value) && value.Length > 0
                ? value
                : defaultValue;
        }

        internal bool GetBool(string key, bool defaultValue)
        {
            return _values.TryGetValue(key, out string value) && bool.TryParse(value, out bool parsed)
                ? parsed
                : defaultValue;
        }

        internal double GetDouble(string key, double defaultValue)
        {
            if (!_values.TryGetValue(key, out string value) ||
                !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) ||
                double.IsNaN(parsed) ||
                double.IsInfinity(parsed))
                return defaultValue;

            return parsed;
        }

        internal void Set(string key, object value)
        {
            if (!_values.ContainsKey(key))
                _keys.Add(key);

            _values[key] = value is IFormattable formattable
                ? formattable.ToString(null, CultureInfo.InvariantCulture)
                : value?.ToString() ?? string.Empty;
        }

        internal void Save()
        {
            string directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string temporaryPath = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var writer = new StreamWriter(temporaryPath, false, new UTF8Encoding(false)))
                    foreach (string key in _keys)
                        writer.WriteLine($"{key}={_values[key]}");

                if (File.Exists(FilePath))
                    File.Replace(temporaryPath, FilePath, null);
                else
                    File.Move(temporaryPath, FilePath);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

    }
}
