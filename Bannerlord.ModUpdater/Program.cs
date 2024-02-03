// See https://aka.ms/new-console-template for more information
using Bannerlord.ModUpdater.Services;
using Octokit;

if (args.Length == 0)
{
    Console.WriteLine("GitHub token must be provided as commandline argument");
    return -1;
}

var token = args[0];

var repoProvider = new RepoProvider();
var repos = await repoProvider.GetRepos();

var client = new GitHubClient(new ProductHeaderValue("Bannerlord.ModUpdater"))
{
    Credentials = new Credentials(token)
};

foreach (var repo in repos)
{
    Console.Write($"Fetching PRs from repository '{repo.Owner}/{repo.Name}'...");
    var pullRequests = await client.PullRequest.GetAllForRepository(repo.Owner, repo.Name);

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
            return -1;
        }
    }
}



return 0;