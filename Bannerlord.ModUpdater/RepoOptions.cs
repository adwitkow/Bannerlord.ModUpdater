using Bannerlord.ModUpdater.Models;

namespace Bannerlord.ModUpdater
{
    internal class RepoOptions
    {
        public const string SectionName = "repoOptions";

        public Repo[] Repos { get; set; } = Array.Empty<Repo>();
    }
}
