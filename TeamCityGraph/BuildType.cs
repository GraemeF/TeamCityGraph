using System.Collections.Immutable;
using System.Linq;

namespace TeamCityGraph
{
    public class BuildType
    {
        public BuildType(string id, string name, IImmutableDictionary<string, Build> builds)
        {
            Name = name;
            Id = id;
            Builds = builds;
        }

        public string Name { get; private set; }
        public string Id { get; private set; }
        public IImmutableDictionary<string, Build> Builds { get; private set; }

        public bool UsesNuGet
        {
            get { return Builds.Any(x => x.Value.UsesNuGet); }
        }

        public bool PublishesNugGetPackages
        {
            get { return Builds.Any(x => x.Value.CreatedPackages.Any()); }
        }

        public override string ToString()
        {
            return string.Format("Name: {0}, Id: {1}", Name, Id);
        }
    }
}