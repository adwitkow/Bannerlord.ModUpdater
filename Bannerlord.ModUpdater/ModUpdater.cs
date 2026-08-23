using Bannerlord.ModUpdater.Cli;
using Bannerlord.ModUpdater.Models;
using Microsoft.Extensions.Options;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using Steamworks;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace Bannerlord.ModUpdater
{
    internal class ModUpdater
    {
        private const string VersionFileName = "supported-game-versions.txt";

        public static string WorkingDirectory =>
            Path.Combine(Directory.GetCurrentDirectory(), "work");

        public static string ReleaseDirectory =>
            Path.Combine(Directory.GetCurrentDirectory(), "release");

        private readonly Octokit.IGitHubClient _githubClient;
        private readonly GitHubAssetClient _assetClient;
        private readonly IEnumerable<Repo> _repos;

        public ModUpdater(
            IOptions<RepoOptions> repoOptions,
            Octokit.IGitHubClient githubClient,
            GitHubAssetClient assetClient)
        {
            _repos = repoOptions.Value.Repos;
            _githubClient = githubClient;
            _assetClient = assetClient;
        }

        public async Task<bool> CheckPullRequests()
        {
            foreach (var repo in _repos)
            {
                Console.Write($"Fetching PRs from repository '{repo.Owner}/{repo.Name}'...");
                var pullRequests = await _githubClient.PullRequest
                    .GetAllForRepository(repo.Owner, repo.Name);

                if (pullRequests.Count == 0)
                {
                    Console.WriteLine("OK");
                }
                else
                {
                    Console.WriteLine($"There's {pullRequests.Count} open PRs.");
                    foreach (var pr in pullRequests)
                    {
                        Console.WriteLine($"- '{pr.Title}' by {pr.User.Login}");
                    }
                    Console.WriteLine($"Do you wish to continue? y/n");

                    var response = Console.ReadKey();
                    Console.WriteLine();
                    if (response.Key != ConsoleKey.Y)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public async Task UpdateAllMods(string gameVersion)
        {
            var owners = _repos.Select(repo => repo.Owner).Distinct();

            foreach (var owner in owners)
            {
                var path = Path.Combine(WorkingDirectory, owner);
                Directory.CreateDirectory(path);
            }

            SteamClient.Init(261550, true);

            await RebuildForGameVersion(gameVersion);

            var metadata = await CollectReleaseMetadata();

            Console.WriteLine("----------");

            if (metadata.Count == 0)
            {
                Console.WriteLine("No commits to publish.");

                SteamClient.Shutdown();

                return;
            }
            else
            {
                foreach (var release in metadata)
                {
                    Console.WriteLine(release.Repo.Name);
                    Console.WriteLine($"{release.OldVersion} -> {release.NewVersion}");
                    Console.WriteLine(string.Join(Environment.NewLine, release.Commits));
                    Console.WriteLine();
                }
            }

            Console.WriteLine($"Approved? y/n");

            var response = Console.ReadKey();
            Console.WriteLine();
            if (response.Key != ConsoleKey.Y)
            {
                return;
            }

            PushReleaseBranches(metadata);

            Console.WriteLine("Going to sleep for a few minutes while releases get published.");
            Console.WriteLine("Repos to publish:");
            foreach (var release in metadata)
            {
                Console.WriteLine(release.Repo.Name);
            }

            await Task.Delay(TimeSpan.FromMinutes(3));
            
            await ReleaseToWorkshop(metadata);

            SteamClient.Shutdown();
        }

        private async Task<List<ReleaseMetadata>> CollectReleaseMetadata()
        {
            var metadata = new List<ReleaseMetadata>();
            foreach (var repo in _repos)
            {
                var repoDirectory = GetWorkingRepoDirectory(repo.Owner, repo.Name);
                var versionFile = Path.Combine(repoDirectory, VersionFileName);
                var versions = File.ReadAllLines(versionFile).ToList();

                var git = new GitRepositoryFacade(repo.Owner, repo.Name, WorkingDirectory);

                var oldVersion = await GetLatestReleaseVersion(repo);
                var newVersion = await CalculateReleaseVersion(repo, oldVersion);

                var commits = git.GetCommitsSinceLastRelease();

                if (commits.Length == 0)
                {
                    if (!string.IsNullOrEmpty(repo.ForcedVersion))
                    {
                        commits = ["Update version"];
                    }
                    else
                    {
                        continue;
                    }
                }

                metadata.Add(new ReleaseMetadata()
                {
                    Repo = repo,
                    OldVersion = oldVersion,
                    NewVersion = newVersion,
                    SupportedGameVersions = versions,
                    Commits = commits,
                });
            }

            return metadata;
        }

        private async Task ReleaseToWorkshop(List<ReleaseMetadata> metaDataList)
        {
            while (metaDataList.Count != 0)
            {
                for (int i = metaDataList.Count - 1; i >= 0; i--)
                {
                    var metaData = metaDataList[i];

                    var repo = metaData.Repo;
                    var tagName = metaData.GetPrefixedNewVersion();
                    var versions = metaData.SupportedGameVersions;

                    Console.WriteLine($"Checking if '{repo.Owner}/{repo.Name}' has released");
                    var latestRelease = await _githubClient.Repository.Release
                        .GetLatest(repo.Owner, repo.Name);
                    if (latestRelease.TagName == tagName)
                    {
                        Console.WriteLine($"Release {tagName} found, extracting...");
                        metaData.ReleaseUrl = await ExtractReleaseAssets(repo, latestRelease);

                        Console.WriteLine($"Publishing to workshop...");
                        await PublishToWorkshop(metaData);

                        metaDataList.RemoveAt(i);
                    }
                }

                if (metaDataList.Count != 0)
                {
                    await Task.Delay(TimeSpan.FromMinutes(1));
                }
            }
        }

        private async Task RebuildForGameVersion(string gameVersion)
        {
            foreach (var repo in _repos)
            {
                var directoryWithOwner = Path.Combine(WorkingDirectory, repo.Owner);
                var repoDirectory = GetWorkingRepoDirectory(repo.Owner, repo.Name);
                var projectDirectory = Path.Combine(repoDirectory, repo.Name);

                var git = new GitRepositoryFacade(repo.Owner, repo.Name, WorkingDirectory);
                var dotnet = new DotnetFacade(projectDirectory);

                if (!Directory.Exists(repoDirectory))
                {
                    git.Clone();
                }

                //TODO: The master branch name should be configurable
                //      for the perverts that use 'main'
                git.CheckoutBranch("master");
                git.Pull();

                await UpdateSdkAsync(projectDirectory, git);

                var versionFile = Path.Combine(repoDirectory, VersionFileName);
                var versions = File.ReadAllLines(versionFile).ToList();

                var versionUpdated = UpdateVersionFile(versions, gameVersion, versionFile);
                if (!versionUpdated)
                {
                    Console.WriteLine($"{repo.Owner}/{repo.Name} already contains version {gameVersion}.");
                }

                dotnet.BuildProject(gameVersion);

                if (versionUpdated)
                {
                    git.StageAll();
                    git.Commit($"Add v{gameVersion} to supported game versions.");
                }

                var outdatedPackages = dotnet.GetOutdatedPackages();
                foreach (var package in outdatedPackages)
                {
                    dotnet.UpdatePackage(package.Id);
                    dotnet.BuildProject(gameVersion);

                    git.StageAll();
                    git.Commit($"Update {package.Id} to {package.LatestVersion}.");
                }

                git.Push();
            }
        }

        private async Task UpdateSdkAsync(string projectDirectory, GitRepositoryFacade git)
        {
            const string sdk = "Bannerlord.BuildResourcesLite.Sdk";

            var csproj = Directory
                .EnumerateFiles(projectDirectory, "*.csproj", SearchOption.AllDirectories)
                .Single();

            var repo = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
            var resource = await repo.GetResourceAsync<FindPackageByIdResource>();

            using var cacheContext = new SourceCacheContext();

            var versions = await resource.GetAllVersionsAsync(
                sdk,
                cacheContext,
                NullLogger.Instance,
                CancellationToken.None);

            var latest = versions
                .Where(v => !v.IsPrerelease)
                .Max();

            var content = await File.ReadAllTextAsync(csproj);

            var updated = Regex.Replace(
                content,
                $@"({Regex.Escape(sdk)}/)[\d.]+",
                $"{sdk}/{latest}");

            if (!string.Equals(content, updated))
            {
                await File.WriteAllTextAsync(csproj, updated);

                git.StageAll();
                git.Commit($"Update {sdk} to {latest}.");
            }
        }

        public void PushReleaseBranches(List<ReleaseMetadata> metadata)
        {
            foreach (var release in metadata)
            {
                var repo = release.Repo;
                var git = new GitRepositoryFacade(repo.Owner, repo.Name, WorkingDirectory);

                var newBranch = $"release/{release.NewVersion}";
                try
                {
                    git.CheckoutNewBranch(newBranch);
                }
                catch { } // will throw an exception if branch already exists locally
                git.PushBranch(newBranch);
            }
        }

        private static async Task<bool> PublishToWorkshop(ReleaseMetadata metaData)
        {
            var repo = metaData.Repo;
            var releaseVersion = metaData.GetPrefixedNewVersion();
            var supportedVersions = metaData.SupportedGameVersions;

            string[] changeLogLines = [$"Version {releaseVersion}", $"Changelog: {metaData.ReleaseUrl}"];
            var changeLog = string.Join(Environment.NewLine, changeLogLines);

            var releaseDirectory = GetReleaseRepoDirectory(repo.Owner, repo.Name);
            var buildPath = Path.Combine(releaseDirectory, "Modules", repo.Name);
            var editor = new Steamworks.Ugc.Editor(repo.WorkshopId)
                .WithContent(new DirectoryInfo(buildPath))
                .WithChangeLog(changeLog);

            foreach (var version in supportedVersions)
            {
                editor.WithTag(version);
            }

            foreach (var tag in repo.Tags)
            {
                editor.WithTag(tag);
            }

            var result = await editor.SubmitAsync();

            if (!result.Success)
            {
                Console.WriteLine($"Publish failed with code {(int)result.Result} ({result.Result})");
            }
            else
            {
                Console.WriteLine($"Publish succeeded.");
            }

            return result.Success;
        }

        private async Task<string> ExtractReleaseAssets(Repo repo, Octokit.Release release)
        {
            var asset = release.Assets[0].Url;

            using var stream = await _assetClient.GetAssetStream(asset);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

            var directory = GetReleaseRepoDirectory(repo.Owner, repo.Name);
            archive.ExtractToDirectory(directory);

            return release.HtmlUrl;
        }

        private async Task<string> GetLatestReleaseVersion(Repo repo)
        {
            string version;
            try
            {
                var latestRelease = await _githubClient.Repository.Release
                    .GetLatest(repo.Owner, repo.Name);
                version = latestRelease.TagName.Substring(1);
            }
            catch (Octokit.NotFoundException)
            {
                version = "1.0.0.-1";
            }
            
            return version;
        }

        private async Task<string> CalculateReleaseVersion(Repo repo, string oldVersion)
        {
            if (!string.IsNullOrWhiteSpace(repo.ForcedVersion))
            {
                return repo.ForcedVersion;
            }

            var subversions = oldVersion.Split('.')
                .Select(s => Convert.ToInt32(s))
                .ToArray();

            var subversionsLength = subversions.Length;
            if (subversionsLength < 4)
            {
                Array.Resize(ref subversions, 4);
            }

            var indexToIncrease = repo.Release switch
            {
                ReleaseType.Major => 0,
                ReleaseType.Minor => 1,
                ReleaseType.Patch => 2,
                ReleaseType.Rebuild => 3,
                _ => throw new NotImplementedException(),
            };

            subversions[indexToIncrease]++;

            for (int i = 3; i > indexToIncrease; i--)
            {
                subversions[i] = 0;
            }

            return string.Join('.', subversions);
        }

        private static bool UpdateVersionFile(List<string> versions, string gameVersion, string versionFile)
        {
            var formattedVersion = $"v{gameVersion}";
            var toUpdate = !versions.Contains(formattedVersion);

            if (toUpdate)
            {
                versions.Add(formattedVersion);
                var orderedVersions = versions
                    .OrderByDescending(GetMajor)
                    .ThenByDescending(GetMinor)
                    .ThenByDescending(GetPatch)
                    .ToList();

                RemoveOldInterimVersions(orderedVersions);

                File.WriteAllLines(versionFile, orderedVersions);
            }

            return toUpdate;
        }

        public static void RemoveOldInterimVersions(List<string> versions)
        {
            if (versions.Count == 0)
            {
                return;
            }

            // List sorted in descending order
            int highestMinor = GetMinor(versions[0]);
            int previousMinor = -1;
            for (int i = versions.Count - 1; i > 0; i--)
            {
                var currentMinor = GetMinor(versions[i]);
                var nextMinor = GetMinor(versions[i - 1]);

                var isFirstVersion = previousMinor != currentMinor;
                var isLastVersion = nextMinor != currentMinor;
                var isHighestVersion = highestMinor == currentMinor;

                if (!isFirstVersion && !isLastVersion && !isHighestVersion)
                {
                    versions.RemoveAt(i);
                }

                previousMinor = currentMinor;
            }
        }

        private static int GetMajor(string s)
        {
            const int versionMarkerOffset = 1;

            int p1 = s.IndexOf('.');
            int p2 = s.IndexOf('.', p1 + 1);
            return int.Parse(s.AsSpan(versionMarkerOffset, p1 - versionMarkerOffset));
        }

        private static int GetMinor(string s)
        {
            int p1 = s.IndexOf('.');
            int p2 = s.IndexOf('.', p1 + 1);
            return int.Parse(s.AsSpan(p1 + 1, p2 - p1 - 1));
        }

        private static int GetPatch(string s)
        {
            int p1 = s.IndexOf('.');
            int p2 = s.IndexOf('.', p1 + 1);
            return int.Parse(s.AsSpan(p2 + 1));
        }

        private static string GetWorkingRepoDirectory(string owner, string name)
        {
            return Path.Combine(WorkingDirectory, owner, name);
        }

        private static string GetReleaseRepoDirectory(string owner, string name)
        {
            return Path.Combine(ReleaseDirectory, owner, name);
        }
    }
}
