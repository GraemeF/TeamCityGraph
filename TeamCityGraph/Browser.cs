using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace TeamCityGraph
{
    public class Browser
    {
        private static readonly XDocument NoPackages =
            new XDocument(new XElement("nuget-dependencies",
                                       new object[]
                                       {
                                           new XElement("packages"),
                                           new XElement("created"),
                                           new XElement("published")
                                       }));

        private readonly HttpClient client;
        private readonly Func<PackageVersionId, bool> packageFilter;

        public Browser(HttpClient client, Func<PackageVersionId, bool> packageFilter)
        {
            this.client = client;
            this.packageFilter = packageFilter;
        }

        public Browser(HttpClient client)
            : this(client, package => true)
        {
        }

        private async Task<HttpResponseMessage> FollowLink(HttpResponseMessage response, IEnumerable<string> path)
        {
            return await GetAsync(await GetLink(response, path));
        }

        private Task<HttpResponseMessage> GetAsync(Uri requestUri)
        {
            Debug.WriteLine("GET {0}", requestUri);
            return this.client.GetAsync(requestUri);
        }

        private async Task<Uri> GetLink(HttpResponseMessage response, IEnumerable<string> path)
        {
            return GetLink(await response.GetXmlFromResponse(), path);
        }

        private Uri GetLink(XDocument doc, IEnumerable<string> path)
        {
            return GetUriFromHref(NavigateToElement(doc, path).Attribute("href").Value);
        }

        private static XElement NavigateToElement(XDocument doc, IEnumerable<string> elements)
        {
            return elements.Aggregate(doc.Root, (element, next) => element.Element(next));
        }

        private Uri GetUriFromHref(string relativeUri)
        {
            return new Uri(this.client.BaseAddress, relativeUri);
        }

        private async Task<XDocument> GetProjectsList()
        {
            return await (await FollowLink(await this.client.GetAsync(""), new[] { "projects" }))
                .GetXmlFromResponse();
        }

        public async Task<IEnumerable<Task<Project>>> GetProjects()
        {
            return (await GetProjectsList())
                .Root
                .Elements("project")
                .Select(async project => new Project(project.Attribute("id").Value,
                                                     project.Attribute("name").Value,
                                                     (await Task.WhenAll(await GetBuildTypesFromProjectRef(project)))
                                                         .ToImmutableDictionary(x => x.Id)));
        }

        private async Task<IEnumerable<Task<BuildType>>> GetBuildTypesFromProjectRef(XElement projectRef)
        {
            XDocument project = await FollowLink(projectRef);
            return project.Root
                          .Element("buildTypes")
                          .Elements("buildType")
                          .Select(async buildTypeRef =>
                                      new BuildType(buildTypeRef.Attribute("id").Value,
                                                    buildTypeRef.Attribute("name").Value,
                                                    (await
                                                        Task.WhenAll(
                                                            await GetLastSuccessfulBuildFromBuildTypeRef(buildTypeRef)))
                                                        .ToImmutableDictionary(x => x.Id)));
        }

        private async Task<IEnumerable<Task<Build>>> GetLastSuccessfulBuildFromBuildTypeRef(XElement buildTypeRef)
        {
            XDocument buildType = await FollowLink(buildTypeRef);
            var successfulBuildsUri = new UriBuilder(GetUriFromLink(buildType.Root.Element("builds")))
            {
                Query = "status=SUCCESS"
            };

            XDocument successfulBuilds =
                await (await GetAsync(successfulBuildsUri.Uri)).GetXmlFromResponse();

            return successfulBuilds.Root
                                   .Elements("build")
                                   .Take(1)
                                   .Select(async buildRef => await CreateBuild(buildRef));
        }

        private async Task<Build> CreateBuild(XElement buildRef)
        {
            XDocument packagesDoc = await GetPackagesDoc(buildRef);
            Package[] dependencies = await Task.WhenAll(GetPackagesFromBuildRef(packagesDoc, "packages"));
            Package[] createdPackages = await Task.WhenAll(GetPackagesFromBuildRef(packagesDoc, "created"));

            return new Build(buildRef.Attribute("id").Value,
                             buildRef.Attribute("number").Value,
                             createdPackages.ToImmutableDictionary(x => x.VersionId),
                             dependencies.ToImmutableDictionary(x => x.VersionId));
        }

        private async Task<XDocument> GetPackages(string buildTypeId, string buildId)
        {
            var requestUri = new Uri(this.client.BaseAddress,
                                     string.Format(
                                         "/repository/download/{0}/{1}:id/.teamcity/nuget/nuget.xml",
                                         buildTypeId,
                                         buildId));

            HttpResponseMessage response = await GetAsync(requestUri);
            return response.IsSuccessStatusCode
                ? XDocument.Load(await response.Content.ReadAsStreamAsync())
                : NoPackages;
        }

        private async Task<XDocument> GetPackagesDoc(XElement buildRef)
        {
            return await GetPackages(buildRef.Attribute("buildTypeId").Value, buildRef.Attribute("id").Value);
        }

        private IEnumerable<Task<Package>> GetPackagesFromBuildRef(XDocument packages, string relation)
        {
            return
                packages.Root.Element(relation)
                        .Elements("package")
                        .Select(PackageVersionId.FromElement)
                        .Where(id => this.packageFilter(id))
                        .Select(async id => new Package(id, (await GetPackageDependencies(id)).Memoize()));
        }

        private async Task<IEnumerable<PackageVersionId>> GetPackageDependencies(PackageVersionId id)
        {
            XNamespace m = "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata";
            XNamespace d = "http://schemas.microsoft.com/ado/2007/08/dataservices";

            try
            {
                XDocument doc = await (await GetAsync(GetPackageFeedUri(id))).GetXmlFromResponse();
                string dependencies = doc.Root.Element(m + "properties").Element(d + "Dependencies").Value;
                return dependencies
                    .Split('|')
                    .Where(x => !string.IsNullOrEmpty(x))
                    .Select(PackageVersionId.FromFeedDependency)
                    .Where(this.packageFilter);
            }
            catch (Exception)
            {
                Debug.WriteLine("Failed to get package {0}.", id);
                return Enumerable.Empty<PackageVersionId>();
            }
        }

        private static Uri GetPackageFeedUri(PackageVersionId id)
        {
            return new Uri(
                string.Format(
                    "http://teamcity/guestAuth/app/nuget/v1/FeedService.svc/Packages(Id='{0}',Version='{1}')",
                    id.Id,
                    id.Version));
        }

        private async Task<XDocument> FollowLink(XElement projectRef)
        {
            return await (await GetAsync(GetUriFromLink(projectRef))).GetXmlFromResponse();
        }

        private Uri GetUriFromLink(XElement link)
        {
            return GetUriFromHref(link.Attribute("href").Value);
        }
    }
}