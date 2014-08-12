using System.Collections.Immutable;
using System.Linq;

namespace TeamCityGraph
{
    public class Build
    {
        public Build(string id,
                     string number,
                     IImmutableDictionary<PackageVersionId, Package> createdPackages,
                     IImmutableDictionary<PackageVersionId, Package> dependencies)
        {
            CreatedPackages = createdPackages;
            Dependencies = dependencies;
            Id = id;
            Number = number;
        }

        public string Id { get; private set; }
        public string Number { get; private set; }

        public bool UsesNuGet
        {
            get { return CreatedPackages.Any() || Dependencies.Any(); }
        }

        public IImmutableDictionary<PackageVersionId, Package> Dependencies { get; private set; }
        public IImmutableDictionary<PackageVersionId, Package> CreatedPackages { get; private set; }

        public override string ToString()
        {
            return string.Format("Id: {0}, Number: {1}", Id, Number);
        }
    }
}