using System.Diagnostics;
using System.IO.Compression;
using Bannerlord.ModUpdater.Models;
using Microsoft.Extensions.Options;
using Octokit;
using Steamworks;

namespace Bannerlord.ModUpdater
{
    internal class ModUpdater
    {
        private const string VersionFileName = "supported-game-versions.txt";

        public static string WorkingDirectory =>
            Path.Combine(Directory.GetCurrentDirectory(), "work");

        public static string ReleaseDirectory =>
            Path.Combine(Directory.GetCurrentDirectory(), "release");

        private readonly IGitHubClient _githubClient;
        private readonly GitHubAssetClient _assetClient;
        private readonly IEnumerable<Repo> _repos;

        public ModUpdater(
            IOptions<RepoOptions> repoOptions,
            IGitHubClient githubClient,
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
            var metaDataList = new List<RepoMetaData>();

            if (!Directory.Exists(WorkingDirectory))
            {
                var owners = _repos.Select(repo => repo.Owner).Distinct();

                foreach (var owner in owners)
                {
                    var path = Path.Combine(WorkingDirectory, owner);
                    Directory.CreateDirectory(path);
                }
            }

            if (Directory.Exists(ReleaseDirectory))
            {
                Directory.Delete(ReleaseDirectory, true);
            }

            SteamClient.Init(261550, true);

            foreach (var repo in _repos)
            {
                var git = new GitRepositoryFacade(repo.Owner, repo.Name, WorkingDirectory);

                var directoryWithOwner = Path.Combine(WorkingDirectory, repo.Owner);
                await git.Clone();

                var repoDirectory = GetWorkingRepoDirectory(repo.Owner, repo.Name);

                //TODO: The master branch name should be configurable
                //      for the perverts that use 'main'
                await git.CheckoutBranch("master");
                await git.Pull();

                var versionFile = Path.Combine(repoDirectory, VersionFileName);
                var versions = File.ReadAllLines(versionFile).ToList();
                if (!UpdateVersionFile(versions, gameVersion, versionFile))
                {
                    Console.WriteLine($"{repo.Owner}/{repo.Name} already contains version {gameVersion}.");
                }

                await git.StageAll();

                await BuildProject(repoDirectory);

                await git.Commit($"Add v{gameVersion} to supported game versions");
                await git.Push();

                var latestVersion = await GetLatestReleaseVersion(repo);
                var newVersion = CalculateReleaseVersion(latestVersion);

                var commits = await git.GetCommitsSinceLastRelease();

                if (commits.Length != 0)
                {
                    metaDataList.Add(new RepoMetaData()
                    {
                        Repo = repo,
                        NewVersion = $"v{newVersion}",
                        SupportedGameVersions = versions,
                        Commits = commits,
                    });

                    var newBranch = $"release/{newVersion}";
                    await git.CheckoutNewBranch($"release/{newVersion}");
                    await git.PushBranch(newBranch);
                }
            }

            if (metaDataList.Count == 0)
            {
                Console.WriteLine("No commits to publish.");

                SteamClient.Shutdown();

                return;
            }

            Console.WriteLine("Going to sleep for a few minutes while releases get published.");
            await Task.Delay(TimeSpan.FromMinutes(3));

            while (metaDataList.Count != 0)
            {
                for (int i = metaDataList.Count - 1; i >= 0; i--)
                {
                    var metaData = metaDataList[i];

                    var repo = metaData.Repo;
                    var tagName = metaData.NewVersion;
                    var versions = metaData.SupportedGameVersions;

                    Console.WriteLine($"Checking if '{repo.Owner}/{repo.Name}' has released");
                    var latestRelease = await _githubClient.Repository.Release
                        .GetLatest(repo.Owner, repo.Name);
                    if (latestRelease.TagName == tagName)
                    {
                        Console.WriteLine($"Release {tagName} found, extracting...");
                        await ExtractReleaseAssets(repo, latestRelease);

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

            SteamClient.Shutdown();
        }

        private static async Task<bool> PublishToWorkshop(RepoMetaData metaData)
        {
            var repo = metaData.Repo;
            var releaseVersion = metaData.NewVersion;
            var supportedVersions = metaData.SupportedGameVersions;

            var changeLogLines = metaData.Commits.Prepend($"Version {releaseVersion}");
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

        private async Task ExtractReleaseAssets(Repo repo, Release release)
        {
            var asset = release.Assets[0].Url;

            using var stream = await _assetClient.GetAssetStream(asset);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

            var directory = GetReleaseRepoDirectory(repo.Owner, repo.Name);
            archive.ExtractToDirectory(directory);
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
            catch (NotFoundException)
            {
                version = "1.0.0.-1";
            }
            
            return version;
        }

        private static bool UpdateVersionFile(List<string> versions, string gameVersion, string versionFile)
        {
            var formattedVersion = $"v{gameVersion}";
            var toUpdate = !versions.Contains(formattedVersion);

            if (toUpdate)
            {
                versions.Insert(0, formattedVersion);
                File.WriteAllLines(versionFile, versions);
            }

            return toUpdate;
        }

        private static async Task BuildProject(string repoDirectory)
        {
            var buildProcess = Process.Start(new ProcessStartInfo()
            {
                WorkingDirectory = repoDirectory,
                FileName = "dotnet",
                Arguments = "build"
            });

            if (buildProcess is null)
            {
                throw new InvalidOperationException("Could not run 'dotnet build'");
            }

            await buildProcess.WaitForExitAsync();

            if (buildProcess.ExitCode != 0)
            {
                throw new Exception("The project has failed to be built.");
            }
        }

        private static string CalculateReleaseVersion(string latestVerrsion)
        {
            var subversions = latestVerrsion.Split('.')
                .Select(s => Convert.ToInt32(s))
                .ToArray();

            var subversionsLength = subversions.Length;
            if (subversionsLength < 4)
            {
                Array.Resize(ref subversions, 4);
            }

            subversions[3]++;

            return string.Join('.', subversions);
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
