// See https://aka.ms/new-console-template for more information
using Bannerlord.ModUpdater;
using Bannerlord.ModUpdater.Models;
using Cocona;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

const string UserAgent = "Bannerlord.ModUpdater";

if (Directory.Exists(ModUpdater.WorkingDirectory))
{
    ForceDeleteDirectory(ModUpdater.WorkingDirectory);
}

if (Directory.Exists(ModUpdater.ReleaseDirectory))
{
    ForceDeleteDirectory(ModUpdater.ReleaseDirectory);
}

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

static void ForceDeleteDirectory(string path, int maxRetries = 10)
{
    if (!Directory.Exists(path))
        return;

    // Remove read-only/system attributes recursively
    NormalizeAttributes(path);

    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            Directory.Delete(path, recursive: true);
            return;
        }
        catch (UnauthorizedAccessException)
        {
            NormalizeAttributes(path);
            Thread.Sleep(500);
        }
        catch (IOException)
        {
            // Often "file in use"
            Thread.Sleep(500);
        }
    }

    throw new Exception($"Failed to delete directory: {path}");
}

static void NormalizeAttributes(string path)
{
    foreach (string dir in Directory.GetDirectories(path, "*", SearchOption.AllDirectories))
    {
        File.SetAttributes(dir, FileAttributes.Normal);
    }

    foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
    {
        File.SetAttributes(file, FileAttributes.Normal);
    }

    File.SetAttributes(path, FileAttributes.Normal);
}