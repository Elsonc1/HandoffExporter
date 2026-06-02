using System.IO;
using System.Xml.Serialization;

namespace HandoffExporter.Xml
{
    public class XmlHelper : IXmlHelper
    {
        public T XmlDeserialize<T>(string xml)
        {
            var serializer = new XmlSerializer(typeof(T));
            using (var reader = new StringReader(xml))
            {
                return (T)serializer.Deserialize(reader);
            }
        }

        public string XmlSerialize<T>(T obj, bool indent)
        {
            var serializer = new XmlSerializer(typeof(T));
            using (var writer = new StringWriter())
            {
                serializer.Serialize(writer, obj);
                return writer.ToString();
            }
        }
    }
}