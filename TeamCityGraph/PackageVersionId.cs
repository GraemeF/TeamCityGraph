using System.Xml.Linq;

namespace TeamCityGraph
{
    public class PackageVersionId
    {
        public PackageVersionId(string id, string version)
        {
            Id = id;
            Version = version;
        }

        public string Id { get; set; }
        public string Version { get; set; }

        public static PackageVersionId FromElement(XElement package)
        {
            return new PackageVersionId(package.Attribute("id").Value,
                                        package.Attribute("version").Value);
        }

        public override string ToString()
        {
            return string.Format("Id: {0}, Version: {1}", Id, Version);
        }

        public static PackageVersionId FromFeedDependency(string dependency)
        {
            string[] split = dependency.Split(':');
            return new PackageVersionId(split[0], split[1]);
        }
    }
}