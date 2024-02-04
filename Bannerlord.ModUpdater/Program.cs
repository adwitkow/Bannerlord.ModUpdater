// See https://aka.ms/new-console-template for more information
using Bannerlord.ModUpdater;
using Cocona;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Octokit;
using Octokit.Internal;

var builder = CoconaApp.CreateBuilder();

builder.Services.AddTransient<ModUpdater>();
builder.Services.AddOctoKit(builder.Configuration, "Bannerlord.ModUpdater");

builder.Services.Configure<RepoOptions>(
    builder.Configuration.GetSection(RepoOptions.SectionName));

var app = builder.Build();
app.Run(async (ModUpdater updater, [Option('g')]string gameVersion) =>
{
    Console.WriteLine(gameVersion);
    if (!await updater.CheckPullRequests())
    {
        return -1;
    }

    return 0;
});

public static class Extensions
{
    public static IServiceCollection AddOctoKit(
        this IServiceCollection services,
        IConfiguration configuration,
        string userAgent)
    {
        var token = configuration.GetValue<string>("token");
        services.AddSingleton<IConnection>(_ =>
        {
            var socketsHttpHandler = new SocketsHttpHandler();
            return new Connection(
                new ProductHeaderValue(userAgent),
                new Uri("https://api.github.com"),
                new InMemoryCredentialStore(new Credentials(token)),
                new HttpClientAdapter(() => socketsHttpHandler),
                new SimpleJsonSerializer());
        });

        services.AddTransient<IGitHubClient, GitHubClient>();

        return services;
    }
}