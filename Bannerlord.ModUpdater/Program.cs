// See https://aka.ms/new-console-template for more information
using Bannerlord.ModUpdater;
using Bannerlord.ModUpdater.Models;
using Cocona;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

const string UserAgent = "Bannerlord.ModUpdater";

var builder = CoconaApp.CreateBuilder();

builder.Services.AddOptions();
var repoOptionsSection = builder.Configuration.GetSection(RepoOptions.SectionName);
builder.Services.Configure<RepoOptions>(repoOptionsSection);

var token = builder.Configuration.GetValue<string>("token");
if (string.IsNullOrEmpty(token))
{
    throw new InvalidOperationException("Token is missing from the configuration.");
}

builder.Services.AddTransient<ModUpdater>();
builder.Services.AddScuffedGithubClient(token, UserAgent);
builder.Services.AddOctoKit(token, UserAgent);

var app = builder.Build();
app.Run(async (ModUpdater updater, [Option('g')]string gameVersion) =>
{
    Console.WriteLine(gameVersion);
    if (!await updater.CheckPullRequests())
    {
        return -1;
    }

    await updater.UpdateAllMods(gameVersion);

    return 0;
});