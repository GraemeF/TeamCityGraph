using System.Collections.Generic;

namespace TeamCityGraph
{
    public class Package
    {
        public Package(PackageVersionId versionId, IEnumerable<PackageVersionId> dependencies)
        {
            VersionId = versionId;
            Dependencies = dependencies;
        }

        public PackageVersionId VersionId { get; private set; }
        public IEnumerable<PackageVersionId> Dependencies { get; private set; }

        public override string ToString()
        {
            return VersionId.ToString();
        }
    }
}