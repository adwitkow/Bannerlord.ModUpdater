namespace Bannerlord.ModUpdater.Models
{
    internal class RepoMetaData
    {
        public required Repo Repo { get; init; }

        public required string NewVersion { get; init; }

        public required IEnumerable<string> SupportedGameVersions { get; init; }

        public string? ReleaseUrl { get; set; }
    }
}
