using HandoffExporter.Xml;
using System.IO;
using System.Reflection;

namespace HandoffExporter.Config
{
    public class ConfigManager : IConfigManager
    {
        ConfigVO _config;
        IXmlHelper _utils;

        public ConfigManager(IXmlHelper xmlHelper)
        {
            _utils = xmlHelper;

            _config = new ConfigVO();

            string configContent = File.ReadAllText(GetConfigFilePath());
            _config = _utils.XmlDeserialize<ConfigVO>(configContent);
        }

        public ConfigVO GetConfig() => _config;

        public ConfigVO GetUpdatedConfig()
        {
            string configContent = File.ReadAllText(GetConfigFilePath());
            return _utils.XmlDeserialize<ConfigVO>(configContent);
        }

        public void SetConfig(ConfigVO config)
        {
            _config = config;

            string configContent = _utils.XmlSerialize<ConfigVO>(_config, true);
            File.WriteAllText(GetConfigFilePath(), configContent);
        }

        private string GetConfigFilePath()
        {
            string appFolder = Directory.GetParent(Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName).FullName;
            return Path.Combine(appFolder, "config", "config.xml");
        }
    }
}