namespace Bannerlord.ModUpdater.Models
{
    internal class RepoOptions
    {
        public const string SectionName = "repoOptions";

        public Repo[] Repos { get; set; } = [];
    }
}
