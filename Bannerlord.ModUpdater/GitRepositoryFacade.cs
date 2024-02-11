using System.Diagnostics;
using Octokit;

namespace Bannerlord.ModUpdater
{
    public class GitRepositoryFacade
    {
        private const string RepoUrl = "https://github.com/{0}/{1}.git";

        private const string GitCommand = "git";
        private const string CloneArgument = "clone {0}";
        private const string StageArgument = "add .";
        private const string CommitArgument = "commit -m \"{0}\"";
        private const string CheckoutBranchArgument = "checkout {0}";
        private const string CheckoutNewBranchArgument = "checkout -b {0}";
        private const string PushArgument = "push";
        private const string PushBranchArgument = "push -u origin {0}";
        private const string PullArgument = "pull";
        private const string FancyLogArgument = "log {0}..HEAD --no-merges --oneline --no-decorate --pretty=format:\"* %s\"";
        private const string DescribeLastTagArgument = "describe --tags --abbrev=0";

        private static readonly char[] NewLineChars = Environment.NewLine.ToCharArray();

        private readonly string _owner;
        private readonly string _repoName;
        private readonly string _workingDirectory;

        private string DirectoryWithOwner => Path.Combine(_workingDirectory, _owner);

        private string RepoDirectory => Path.Combine(DirectoryWithOwner, _repoName);

        public GitRepositoryFacade(string owner, string repoName, string workingDirectory)
        {
            _owner = owner;
            _repoName = repoName;
            _workingDirectory = workingDirectory;
        }

        public Task Clone()
        {
            var url = string.Format(RepoUrl, _owner, _repoName);

            var formattedArgument = string.Format(CloneArgument, url);
            return RunGitCommand(DirectoryWithOwner, formattedArgument);
        }

        public Task StageAll()
        {
            return RunGitCommand(RepoDirectory, StageArgument);
        }

        public Task Commit(string commitMessage)
        {
            var formattedArgument = string.Format(CommitArgument, commitMessage);
            return RunGitCommand(RepoDirectory, formattedArgument);
        }

        public Task CheckoutBranch(string branch)
        {
            var formattedArgument = string.Format(CheckoutBranchArgument, branch);
            return RunGitCommand(RepoDirectory, formattedArgument);
        }

        public Task CheckoutNewBranch(string branch)
        {
            var formattedArgument = string.Format(CheckoutNewBranchArgument, branch);
            return RunGitCommand(RepoDirectory, formattedArgument);
        }

        public Task Push()
        {
            return RunGitCommand(RepoDirectory, PushArgument);
        }

        public Task PushBranch(string branch)
        {
            var formattedArgument = string.Format(PushBranchArgument, branch);
            return RunGitCommand(RepoDirectory, formattedArgument);
        }

        public Task Pull()
        {
            return RunGitCommand(RepoDirectory, PullArgument);
        }

        public async Task<string[]> GetCommitsSinceLastRelease()
        {
            var latestTag = await GetLatestTag();
            var formattedArgument = string.Format(FancyLogArgument, latestTag.Trim());
            var result = await RunGitCommand(RepoDirectory, formattedArgument);

            if (string.IsNullOrEmpty(result))
            {
                return Array.Empty<string>();
            }

            return result.Trim().Split(NewLineChars);
        }

        private Task<string> GetLatestTag()
        {
            return RunGitCommand(RepoDirectory, DescribeLastTagArgument);
        }

        private static async Task<string> RunGitCommand(string workingDirectory, string arguments)
        {
            var processInfo = new ProcessStartInfo
            {
                WorkingDirectory = workingDirectory,
                FileName = GitCommand,
                Arguments = arguments,
                RedirectStandardOutput = true,
            };

            Console.WriteLine($"{GitCommand} {arguments}");

            using var process = Process.Start(processInfo);

            if (process is null)
            {
                throw new InvalidOperationException($"There was an issue while trying to run 'git {arguments}'");
            }

            await process.WaitForExitAsync();

            return await process.StandardOutput.ReadToEndAsync();
        }
    }
}
