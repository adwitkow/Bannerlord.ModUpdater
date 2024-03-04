using Bannerlord.ModUpdater.Models;
using System.Text.Json.Nodes;
using System.Text.Json;

namespace Bannerlord.ModUpdater.Cli
{
    internal class DotnetFacade
    {
        private const string DotnetCommand = "dotnet";

        private const string BuildArgument = "build --configuration Release -p:OverrideGameVersion=v{0}";
        private const string OutdatedPackagesArgument = "list package --outdated --format json";
        private const string AddPackageArgument = "add {0} package {1}";

        private readonly string _workingDirectory;

        private readonly CliExecutor _executor;

        public DotnetFacade(string workingDirectory)
        {
            _workingDirectory = workingDirectory;

            _executor = new CliExecutor(DotnetCommand);
        }

        public void BuildProject(string gameVersion)
        {
            var formattedArgument = string.Format(BuildArgument, gameVersion);
            
            _executor.ExecuteCommand(_workingDirectory, formattedArgument);
        }

        public NugetPackage[] GetOutdatedPackages()
        {
            var json = _executor.ExecuteCommand(_workingDirectory, OutdatedPackagesArgument);

            var root = JsonNode.Parse(json);

            // Absolutely disgusting
            var frameworks = root!["projects"]![0]!["frameworks"];
            if (frameworks is null)
            {
                return Array.Empty<NugetPackage>();
            }

            var packagesNode = frameworks[0]!["topLevelPackages"];
            return JsonSerializer.Deserialize<NugetPackage[]>(packagesNode)!;
        }

        public void UpdatePackage(string package)
        {
            var formattedArgument = string.Format(AddPackageArgument, _workingDirectory, package);
            _executor.ExecuteCommand(_workingDirectory, formattedArgument);
        }
    }
}
