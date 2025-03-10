// <copyright file="Program.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System.Reflection;
using System.Runtime.CompilerServices;
using LibraryVersionsGenerator.Models;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;

namespace LibraryVersionsGenerator;

public class Program
{
    private static Dictionary<string, string> _packageVersions = new Dictionary<string, string>();

    public static async Task Main()
    {
        var thisFilePath = GetSourceFilePathName();
        var solutionFolder = Path.Combine(thisFilePath, "..", "..", "..");
        var packagePropsFile = Path.Combine(solutionFolder, "test", "Directory.Packages.props");
        var project = Project.FromFile(packagePropsFile, new ProjectOptions());

        _packageVersions = project.GetItems("PackageVersion").ToDictionary(x => x.EvaluatedInclude, x => x.DirectMetadata.Single().EvaluatedValue);

        var xUnitFileStringBuilder = new XUnitFileBuilder();
        var buildFileStringBuilder = new BuildFileBuilder();

        xUnitFileStringBuilder.AddAutoGeneratedHeader();
        buildFileStringBuilder.AddAutoGeneratedHeader();

        xUnitFileStringBuilder.BeginClass(classNamespace: "IntegrationTests", className: "LibraryVersions");
        buildFileStringBuilder.BeginClass(classNamespace: string.Empty, className: "LibraryVersions");

        foreach (var packageVersionDefinition in PackageVersionDefinitions.Definitions)
        {
            xUnitFileStringBuilder.BeginTestPackage(packageVersionDefinition.TestApplicationName, packageVersionDefinition.IntegrationName);
            buildFileStringBuilder.BeginTestPackage(packageVersionDefinition.TestApplicationName, packageVersionDefinition.IntegrationName);

            HashSet<string> uniqueVersions = new(packageVersionDefinition.Versions.Count);

            foreach (var version in packageVersionDefinition.Versions)
            {
                var calculatedVersion = EvaluateVersion(packageVersionDefinition.NugetPackageName, version.Version);

                if (uniqueVersions.Add(calculatedVersion))
                {
                    if (version.GetType() == typeof(PackageVersion))
                    {
                        xUnitFileStringBuilder.AddVersion(calculatedVersion);
                        buildFileStringBuilder.AddVersion(calculatedVersion);
                    }
                    else
                    {
                        xUnitFileStringBuilder.AddVersionWithDependencies(calculatedVersion, GetDependencies(version));
                        buildFileStringBuilder.AddVersionWithDependencies(calculatedVersion, GetDependencies(version));
                    }
                }
            }

            xUnitFileStringBuilder.EndTestPackage();
            buildFileStringBuilder.EndTestPackage();
        }

        xUnitFileStringBuilder.EndClass();
        buildFileStringBuilder.EndClass();

        var xUnitFilePath = Path.Combine(solutionFolder, "test", "IntegrationTests", "LibraryVersions.g.cs");
        var buildFilePath = Path.Combine(solutionFolder, "build", "LibraryVersions.g.cs");

        await File.WriteAllTextAsync(xUnitFilePath, xUnitFileStringBuilder.ToString());
        await File.WriteAllTextAsync(buildFilePath, buildFileStringBuilder.ToString());
    }

    private static string GetSourceFilePathName([CallerFilePath] string? callerFilePath = null)
        => callerFilePath ?? string.Empty;

    private static string EvaluateVersion(string packageName, string version)
        => version == "*"
            ? _packageVersions[packageName]
            : version;

    private static Dictionary<string, string> GetDependencies(PackageVersion version)
    {
        return version.GetType()
            .GetProperties()
            .Where(x => x.CustomAttributes.Any(x => x.AttributeType == typeof(PackageDependency)))
            .ToDictionary(
                k => k.GetCustomAttribute<PackageDependency>()!.VariableName,
                v =>
                {
                    var packageName = v.GetCustomAttribute<PackageDependency>()!.PackageName;
                    var packageVersion = (string)v.GetValue(version)!;

                    return EvaluateVersion(packageName, packageVersion);
                });
    }
}
