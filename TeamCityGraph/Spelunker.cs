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
    public class Spelunker
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

        public Spelunker(HttpClient client)
        {
            this.client = client;
        }

        private async Task<HttpResponseMessage> FollowLink(HttpResponseMessage response, IEnumerable<string> path)
        {
            return await this.client.GetAsync(await GetLink(response, path));
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

        public async Task<XDocument> GetProjectsList()
        {
            return await (await FollowLink(await this.client.GetAsync(""), new[] { "projects" }))
                .GetXmlFromResponse();
        }

        public async Task<IEnumerable<Task<XDocument>>> GetAllProjects()
        {
            return (await GetProjectsList())
                .Root
                .Elements("project")
                .Select(project => GetUriFromHref(project.Attribute("href").Value))
                .Select(GetXml);
        }

        public IEnumerable<Task<XDocument>> GetBuildTypes(XDocument project)
        {
            return project.Root
                          .Element("buildTypes")
                          .Elements("buildType")
                          .Select(buildType => GetUriFromHref(buildType.Attribute("href").Value))
                          .Select(GetXml);
        }

        public async Task<IEnumerable<Task<XDocument>>> GetBuilds(XDocument buildType)
        {
            return (await GetBuildRefs(buildType))
                .Root
                .Elements("build")
                .Select(build => GetUriFromHref(build.Attribute("href").Value))
                .Select(GetXml);
        }

        private async Task<XDocument> GetBuildRefs(XDocument buildType)
        {
            return (await GetXml(GetLink(buildType, new[] { "builds" })));
        }

        public async Task<XDocument> GetPackages(string buildTypeId, string buildId)
        {
            HttpResponseMessage response = await this.client.GetAsync(new Uri(this.client.BaseAddress,
                                                                              string.Format(
                                                                                  "/repository/download/{0}/{1}:id/.teamcity/nuget/nuget.xml",
                                                                                  buildTypeId,
                                                                                  buildId)));
            return response.IsSuccessStatusCode
                ? XDocument.Load(await response.Content.ReadAsStreamAsync())
                : NoPackages;
        }

        public async Task<XDocument> GetPackagesDoc(XElement buildRef)
        {
            return await GetPackages(buildRef.Attribute("buildTypeId").Value, buildRef.Attribute("id").Value);
        }

        private async Task<XDocument> GetXml(Uri uri)
        {
            return await (await this.client.GetAsync(uri)).GetXmlFromResponse();
        }

        public async Task DumpProjects()
        {
            IEnumerable<Task<XDocument>> getProjects = await GetAllProjects();

            foreach (var result in getProjects)
                Debug.WriteLine(await result);
        }

        public async Task DumpProjectWithBuildTypes()
        {
            IEnumerable<Task<XDocument>> getProjects = await GetAllProjects();

            foreach (var result in getProjects)
            {
                XDocument project = await result;
                IEnumerable<Task<XDocument>> buildTypes = GetBuildTypes(project);
                Debug.WriteLine("{0}: {1}",
                                project.Root.Attribute("name").Value,
                                string.Join(", ", buildTypes.Select(GetName)));
            }
        }

        public async Task DumpBuilds()
        {
            IEnumerable<Task<XDocument>> projects = await GetAllProjects();

            foreach (var project in projects)
            {
                Debug.WriteLine(GetName(project));
                Debug.Indent();

                IEnumerable<Task<XDocument>> buildTypes = GetBuildTypes(await project);

                foreach (var buildType in buildTypes)
                {
                    Debug.WriteLine(GetName(buildType));
                    Debug.Indent();

                    IEnumerable<Task<XDocument>> builds = await GetBuilds(await buildType);

                    foreach (var build in builds)
                        Debug.WriteLine(GetNumber(build));

                    Debug.Unindent();
                }

                Debug.Unindent();
            }
        }

        public async Task DumpPackages()
        {
            IEnumerable<Task<XDocument>> projects = await GetAllProjects();

            foreach (var project in projects)
            {
                Debug.WriteLine(GetName(project));
                Debug.Indent();

                IEnumerable<Task<XDocument>> buildTypes = GetBuildTypes(await project);

                foreach (var buildType in buildTypes)
                {
                    Debug.WriteLine(GetName(buildType));
                    Debug.Indent();

                    XDocument buildRefs = await GetBuildRefs(await buildType);
                    foreach (XElement buildRef in buildRefs.Root.Elements("build"))
                        Debug.WriteLine(await GetPackagesDoc(buildRef));

                    Debug.Unindent();
                }

                Debug.Unindent();
            }
        }

        //public IEnumerable<Project> GetProjects()
        //{
        //    IEnumerable<Task<XDocument>> projects = await GetAllProjects();

        //    foreach (var project in projects)
        //    {
        //        Debug.WriteLine(GetName(project));
        //        Debug.Indent();

        //        IEnumerable<Task<XDocument>> buildTypes = GetBuildTypes(await project);

        //        foreach (var buildType in buildTypes)
        //        {
        //            Debug.WriteLine(GetName(buildType));
        //            Debug.Indent();

        //            XDocument buildRefs = await GetBuildRefs(await buildType);
        //            foreach (XElement buildRef in buildRefs.Root.Elements("build"))
        //                Debug.WriteLine(await GetPackagesDoc(buildRef));

        //            Debug.Unindent();
        //        }

        //        Debug.Unindent();
        //    }
        //}

        //public async Task<IEnumerable<Package>> GetAllPackages()
        //{
        //    XDocument[] buildTypes = await (Task.WhenAll(await GetAllBuildTypes()));
        //    return buildTypes.SelectMany(async project => await GetPackagesForProject(project));
        //}

        public async Task<IEnumerable<Task<XDocument>>> GetAllBuildTypes()
        {
            XDocument[] projects = await Task.WhenAll(await GetAllProjects());
            return projects.SelectMany(GetBuildTypes);
        }

        //private async Task<IEnumerable<Package>> GetPackagesForProject(XDocument project)
        //{
        //    XDocument[] buildTypes = await Task.WhenAll(GetBuildTypes(project));
        //    return buildTypes.SelectMany<Package>(GetPackagesForBuildType);

        //    foreach (XDocument buildType in buildTypes)
        //    {
        //        XDocument buildRefs = await GetBuildRefs(await buildType);
        //        foreach (XElement buildRef in buildRefs.Root.Elements("build"))
        //        {
        //            XDocument packages = await GetPackagesDoc(buildRef);

        //            foreach (XElement package in packages.Root.Element("published").Elements())
        //                new Package(package.Attribute("id").Value,
        //                            package.Attribute("version").Value,
        //                            packages.Root.Element("packages")
        //                                    .Elements("package")
        //                                    .Select(
        //                                        x =>
        //                                            new KeyValuePair<string, string>(x.Attribute("id").Value,
        //                                                                             x.Attribute("version")
        //                                                                              .Value))
        //                                    .ToImmutableDictionary());
        //        }
        //    }
        //}

        private async Task<IEnumerable<Package>> GetPackagesForBuildType(XDocument buildType)
        {
            throw new NotImplementedException();
        }

        private string GetNumber(Task<XDocument> document)
        {
            document.Wait();
            return GetNumber(document.Result);
        }

        private static string GetNumber(XDocument document)
        {
            return document.Root.Attribute("number").Value;
        }

        private static string GetName(XDocument document)
        {
            return document.Root.Attribute("name").Value;
        }

        private static string GetName(Task<XDocument> document)
        {
            document.Wait();
            return GetName(document.Result);
        }
    }
}