using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace TeamCityGraph
{
    internal class Program
    {
        private const string NuGetPackagePrefix = "IgnorePackagesThatDoNotStartWithThis";
        private const string TeamCityUserName = "Bob";
        private const string TeamCityPassword = "B0bR0xx0rz";
        private const string TeamCityRestServerUri = "http://teamcity/app/rest/server";

        private static void Main(string[] args)
        {
            Dump(new Browser(CreateClient(), x => x.Id.StartsWith(NuGetPackagePrefix))).Wait();
        }

        private static async Task Dump(Browser browser)
        {
            const string colorScheme = @"brbg3";
            Console.WriteLine(@"digraph TeamCity {{colorscheme={0};
  node [fontname = ""Helvetica"", style=""rounded,filled"", shape=box, color=1, colorscheme={0}];
  graph [fontname = ""Helvetica-Bold"", style=""rounded,filled"", shape=box, color=3, colorscheme={0}];
  edge [fontname = ""Helvetica""];
  rankdir = LR;
", colorScheme);

            IEnumerable<Task<Project>> projectTasks = (await browser.GetProjects()).ToList();
            foreach (var projectTask in projectTasks)
            {
                Project project = await projectTask;

                if (!project.UsesNuGet)
                    continue;

                Debug.WriteLine("Project {0}", project);
                Debug.Indent();

                Console.WriteLine(@"  subgraph ""cluster_project_{0}"" {{
    label = ""{1}"";", project.Id, project.Name);

                foreach (BuildType buildType in project.BuildTypes.Values)
                {
                    if (!buildType.UsesNuGet)
                        continue;

                    Debug.WriteLine("BuildType {0}", buildType);
                    Debug.Indent();
                    if (buildType.PublishesNugGetPackages)
                        Console.WriteLine(@"    subgraph ""cluster_buildType_{0}"" {{
      label = ""{1}""; color=2;", buildType.Id, buildType.Name);
                    else
                        Console.WriteLine(@"      ""{0}"" [label=""{1}"", color=2];", buildType.Id, buildType.Name);

                    foreach (Build build in buildType.Builds.Values)
                    {
                        if (!build.UsesNuGet)
                            continue;

                        Debug.WriteLine("Build {0}", build);
                        Debug.Indent();

                        foreach (Package package in build.Dependencies.Values)
                            Debug.WriteLine("Dependency {0}", package);

                        foreach (Package package in build.CreatedPackages.Values)
                        {
                            Console.WriteLine(@"        ""{0}"";", package.VersionId.Id);
                            Debug.WriteLine("Created package {0}", package);
                        }
                        Debug.Unindent();
                    }

                    if (buildType.PublishesNugGetPackages)
                        Console.WriteLine("    }");

                    Debug.Unindent();
                }

                Console.WriteLine("  }");

                Debug.Unindent();
            }

            await WriteDependencies(projectTasks);

            Console.WriteLine(@"}");
        }

        private static async Task WriteDependencies(IEnumerable<Task<Project>> projectTasks)
        {
            ImmutableDictionary<string, Package> allPackages =
                (await Task.WhenAll(projectTasks))
                    .SelectMany(project => project.BuildTypes.Values
                                                  .SelectMany(buildType => buildType.Builds.Values
                                                                                    .SelectMany(
                                                                                        build => build.CreatedPackages)))
                    .Distinct(x => x.Key)
                    .ToImmutableDictionary(x => x.Key.Id, x => x.Value);

            foreach (var projectTask in projectTasks)
            {
                Project project = await projectTask;
                foreach (BuildType buildType in project.BuildTypes.Values)
                    foreach (Build build in buildType.Builds.Values)
                        WriteDependencies(buildType, build, allPackages);
            }
        }

        private static void WriteDependencies(BuildType buildType,
                                              Build build,
                                              ImmutableDictionary<string, Package> allPackages)
        {
            if (build.CreatedPackages.Any())
                foreach (Package package in build.CreatedPackages.Values)
                    foreach (string dependency in package.Dependencies
                                                         .Select(x => x.Id).Distinct())
                        Console.WriteLine(@"  ""{0}"" -> ""{1}"";", package.VersionId.Id, dependency);
            else
                foreach (
                    string dependency in
                        build.Dependencies.Select(x => x.Key.Id)
                             .Distinct()
                             .Except(IndirectDependencies(build, allPackages)))
                    Console.WriteLine(@"  ""{0}"" -> ""{1}"";", buildType.Id, dependency);
        }

        private static IEnumerable<string> IndirectDependencies(Build build,
                                                                ImmutableDictionary<string, Package> allPackages)
        {
            return build.Dependencies.Values
                        .SelectMany(x => allPackages[x.VersionId.Id]
                                        .Dependencies
                                        .Select(y => y.Id));
        }

        private static HttpClient CreateClient()
        {
            var handler = new HttpClientHandler
            {
                Credentials = new NetworkCredential(TeamCityUserName, TeamCityPassword)
            };
            var client = new HttpClient(handler)
            {
                BaseAddress = new Uri(TeamCityRestServerUri)
            };

            return client;
        }
    }
}