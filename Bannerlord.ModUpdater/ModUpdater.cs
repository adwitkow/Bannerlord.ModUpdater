using Bannerlord.ModUpdater.Models;
using Microsoft.Extensions.Options;
using Octokit;

namespace Bannerlord.ModUpdater
{
    internal class ModUpdater
    {
        private readonly IGitHubClient _githubClient;
        private readonly IEnumerable<Repo> _repos;

        public ModUpdater(IGitHubClient githubClient, IOptions<RepoOptions> repoOptions)
        {
            _githubClient = githubClient;
            _repos = repoOptions.Value.Repos;
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
    }
}
