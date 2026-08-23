namespace Bannerlord.ModUpdater.Models
{
    internal class ReleaseMetadata
    {
        public required Repo Repo { get; init; }

        public required string OldVersion { get; init; }
        
        public required string NewVersion { get; init; }

        public required IEnumerable<string> SupportedGameVersions { get; init; }

        public required string[] Commits { get; init; }

        public string? ReleaseUrl { get; set; }

        public string GetPrefixedNewVersion()
        {
            return $"v{NewVersion}";
        }
    }
}
