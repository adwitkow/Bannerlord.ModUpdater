namespace Bannerlord.ModUpdater.Cli
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

        private readonly CliExecutor _executor;

        public GitRepositoryFacade(string owner, string repoName, string workingDirectory)
        {
            _owner = owner;
            _repoName = repoName;
            _workingDirectory = workingDirectory;

            _executor = new CliExecutor(GitCommand);
        }

        private string DirectoryWithOwner => Path.Combine(_workingDirectory, _owner);

        private string RepoDirectory => Path.Combine(DirectoryWithOwner, _repoName);

        public void Clone()
        {
            var url = string.Format(RepoUrl, _owner, _repoName);

            var formattedArgument = string.Format(CloneArgument, url);
            _executor.ExecuteCommand(DirectoryWithOwner, formattedArgument);
        }

        public void StageAll()
        {
            _executor.ExecuteCommand(RepoDirectory, StageArgument);
        }

        public void Commit(string commitMessage)
        {
            var formattedArgument = string.Format(CommitArgument, commitMessage);
            _executor.ExecuteCommand(RepoDirectory, formattedArgument);
        }

        public void CheckoutBranch(string branch)
        {
            var formattedArgument = string.Format(CheckoutBranchArgument, branch);
            _executor.ExecuteCommand(RepoDirectory, formattedArgument);
        }

        public void CheckoutNewBranch(string branch)
        {
            var formattedArgument = string.Format(CheckoutNewBranchArgument, branch);
            _executor.ExecuteCommand(RepoDirectory, formattedArgument);
        }

        public void Push()
        {
            _executor.ExecuteCommand(RepoDirectory, PushArgument);
        }

        public void PushBranch(string branch)
        {
            var formattedArgument = string.Format(PushBranchArgument, branch);
            _executor.ExecuteCommand(RepoDirectory, formattedArgument);
        }

        public void Pull()
        {
            _executor.ExecuteCommand(RepoDirectory, PullArgument);
        }

        public string[] GetCommitsSinceLastRelease()
        {
            var latestTag = GetLatestTag();
            var formattedArgument = string.Format(FancyLogArgument, latestTag.Trim());
            var result = _executor.ExecuteCommand(RepoDirectory, formattedArgument);

            if (string.IsNullOrEmpty(result))
            {
                return Array.Empty<string>();
            }

            return result.Trim().Split(NewLineChars, StringSplitOptions.RemoveEmptyEntries);
        }

        private string GetLatestTag()
        {
            return _executor.ExecuteCommand(RepoDirectory, DescribeLastTagArgument);
        }
    }
}
