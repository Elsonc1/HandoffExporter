namespace HandoffExporter.Xml
{
    public interface IXmlHelper
    {
        T XmlDeserialize<T>(string xml);
        string XmlSerialize<T>(T obj, bool indent);
    }
}