namespace HandoffExporter.Config
{
    public interface IConfigManager
    {
        ConfigVO GetConfig();
        ConfigVO GetUpdatedConfig();
        void SetConfig(ConfigVO config);
    }
}