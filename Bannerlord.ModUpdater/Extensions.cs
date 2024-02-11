using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Octokit;
using Octokit.Internal;

namespace Bannerlord.ModUpdater
{
    public static class Extensions
    {
        private static readonly Uri GitHubApiUri = new Uri("https://api.github.com");

        public static IServiceCollection AddOctoKit(
            this IServiceCollection services,
            string token,
            string userAgent)
        {
            services.AddSingleton<IConnection>(_ =>
            {
                var socketsHttpHandler = new SocketsHttpHandler();
                return new Connection(
                    new Octokit.ProductHeaderValue(userAgent),
                    GitHubApiUri,
                    new InMemoryCredentialStore(new Credentials(token)),
                    new HttpClientAdapter(() => socketsHttpHandler),
                    new SimpleJsonSerializer());
            });

            services.AddTransient<IGitHubClient, GitHubClient>();

            return services;
        }

        public static IServiceCollection AddScuffedGithubClient(
            this IServiceCollection services,
            string token,
            string userAgent)
        {
            services.AddHttpClient<GitHubAssetClient>(client =>
            {
                client.BaseAddress = GitHubApiUri;
                var authHeader = new AuthenticationHeaderValue("Bearer", token);
                client.DefaultRequestHeaders.Authorization = authHeader;
                client.DefaultRequestHeaders.UserAgent.TryParseAdd(userAgent);
            });

            return services;
        }
    }
}
