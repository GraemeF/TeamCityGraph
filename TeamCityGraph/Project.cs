using System.Collections.Immutable;
using System.Linq;

namespace TeamCityGraph
{
    public class Project
    {
        public Project(string id, string name, IImmutableDictionary<string, BuildType> buildTypes)
        {
            Name = name;
            Id = id;
            BuildTypes = buildTypes;
        }

        public string Name { get; private set; }
        public string Id { get; private set; }
        public IImmutableDictionary<string, BuildType> BuildTypes { get; private set; }

        public bool UsesNuGet
        {
            get { return BuildTypes.Any(x => x.Value.UsesNuGet); }
        }

        public override string ToString()
        {
            return string.Format("Name: {0}, Id: {1}", Name, Id);
        }
    }
}