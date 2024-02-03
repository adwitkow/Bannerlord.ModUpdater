using System.IO;
using System.Text.Json;
using Bannerlord.ModUpdater.Models;

namespace Bannerlord.ModUpdater.Services
{
    public class RepoProvider
    {
        private const string Path = "repos.json";

        private Repo[]? _repos;

        public async Task<Repo[]> GetRepos()
        {
            if (_repos is null)
            {
                _repos = await InitializeRepos();
            }

            return _repos;
        }

        private async Task<Repo[]> InitializeRepos()
        {
            Repo[]? repos;
            using (var stream = File.Open(Path, FileMode.Open))
            {
                repos = await JsonSerializer.DeserializeAsync<Repo[]>(stream);
            }

            if (repos is null)
            {
                repos = Array.Empty<Repo>();
            }

            return repos;
        }
    }
}
