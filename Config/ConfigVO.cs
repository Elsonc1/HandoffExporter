using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace HandoffExporter.Config
{
    [XmlRoot("ConfigVO")]
    public class ConfigVO
    {
        [XmlElement("ControlTargetDate")]
        public ControlTargetDate ControlTargetDate { get; set; }

        [XmlElement("Key")]
        public string Key { get; set; }

        [XmlElement("Project")]
        public string Project { get; set; }

        [XmlElement("KeyGemini")]
        public string KeyGemini { get; set; }

        public string WebhookUrl { get; }


        private string _configLevel = "INFO";


        [XmlElement("LogLevel")]
        public string LogLevel
        {
            get { return _configLevel; }
            set { _configLevel = ValidateLevel(value); }
        }
        [XmlElement("Time")]
        public int Time { get; set; }

        [XmlElement("Theme")]
        public string Theme { get; set; }

        [XmlElement("Prompt")]
        public string Prompt { get; set; }

        [XmlElement("PromptUs")]
        public string PromptUs { get; set; }

        [XmlElement("LastUpdate")]
        public string LastUpdate { get; set; }

        [XmlElement("AreaPath")]
        public string AreaPath { get; set; }

        [XmlElement("Organization")]
        public string Organization { get; set; }

        [XmlElement("Mode")]
        public string Mode { get; set; }

        [XmlElement("AreaOrId")]
        public string AreaOrId { get; set; }

        [XmlElement("OutputFile")]
        public string OutputFile { get; set; }

        [XmlElement("ReposProject")]
        public string ReposProject { get; set; }

        [XmlIgnore]
        public List<string> AreaNames => ControlTargetDate?.Areas?.Select(a => a.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToList() ?? new List<string>();

        private string ValidateLevel(string level)
        {
            if (level == "DEBUG" || level == "INFO" || level == "WARN" || level == "ERROR")
                return level;
            return "INFO";
        }
    }

    public class ControlTargetDate
    {
        [XmlElement("Date")]
        public string Date { get; set; }

        [XmlArray("Areas")]
        [XmlArrayItem("Area")]
        public List<Area> Areas { get; set; }
    }

    public class Area
    {
        [XmlElement("Name")]
        public string Name { get; set; }

        [XmlElement("Alerta")]
        public string Alerta { get; set; }
    }
}