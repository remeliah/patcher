using System;
using System.IO;
using _patcher.utils;

namespace _patcher
{
    internal class Config : BaseConfig
    {
        internal static string ConfigFileName = "config.ini";

        #region config
        public bool _showMisses { get; private set; } = true;
        public bool _csChange { get; private set; } = false;
        #endregion

        internal static Config _load()
        {
            Config config = new Config();
            DirectoryInfo configDirectory = _getConfigPath();

            if (configDirectory == null)
            {
                return config;
            }

            if (!File.Exists(configDirectory.FullName + "\\" + ConfigFileName))
            {
                _writeToConfig(config, true);
            }
            else
            {
                FileStream configFile = new FileStream(configDirectory.FullName + "\\" + ConfigFileName, FileMode.OpenOrCreate);
                try
                {
                    StreamReader reader = new StreamReader(configFile);
                    try
                    {
                        config._loadConfig(reader);
                    }
                    finally
                    {
                        ((IDisposable)reader).Dispose();
                    }
                }
                finally
                {
                    ((IDisposable)configFile).Dispose();
                }
            }

            return config;
        }

        internal static DirectoryInfo _getConfigPath()
        {
            string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(folderPath)) return null;

            return Directory.CreateDirectory(folderPath + "\\anoPatcher");
        }

        internal static void _writeToConfig(Config config, bool append)
        {
            DirectoryInfo directoryInfo = _getConfigPath();
            if (directoryInfo == null)
            {
                return;
            }

            using (FileStream fileStream = new FileStream(
                directoryInfo.FullName + "\\" + ConfigFileName,
                append ? FileMode.OpenOrCreate : FileMode.Truncate))
            {
                StreamWriter streamWriter = new StreamWriter(fileStream);
                try
                {
                    config._saveConfig(streamWriter);
                    streamWriter.Flush();
                }
                finally
                {
                    ((IDisposable)streamWriter).Dispose();
                }
            }
        }

        public void on_ShowMisses(object sender, EventArgs e) { changeMisses(!PatchRelax()); _writeToConfig(this, false); }
        public bool PatchRelax() => _showMisses; 
        private void changeMisses(bool v) => _showMisses = v;

        public void on_csChange(object sender, EventArgs e) { changecsChange(!csChange()); _writeToConfig(this, false); }
        public bool csChange() => _csChange;
        private void changecsChange(bool v) => _csChange = v;
    }
}