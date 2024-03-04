using System.Text.Json.Serialization;

namespace Bannerlord.ModUpdater.Models
{
    public class NugetPackage
    {
        [JsonConstructor]
        public NugetPackage(
            string id,
            string requestedVersion,
            string resolvedVersion,
            string latestVersion
        )
        {
            Id = id;
            RequestedVersion = requestedVersion;
            ResolvedVersion = resolvedVersion;
            LatestVersion = latestVersion;
        }

        [JsonPropertyName("id")]
        public string Id { get; }

        [JsonPropertyName("requestedVersion")]
        public string RequestedVersion { get; }

        [JsonPropertyName("resolvedVersion")]
        public string ResolvedVersion { get; }

        [JsonPropertyName("latestVersion")]
        public string LatestVersion { get; }
    }

}
