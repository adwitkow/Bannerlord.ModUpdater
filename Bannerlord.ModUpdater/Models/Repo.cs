using System.Text.Json.Serialization;

namespace Bannerlord.ModUpdater.Models
{
    public class Repo()
    {
        [JsonPropertyName("owner")]
        public string Owner { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("workshopId")]
        public ulong WorkshopId { get; set; }

        [JsonPropertyName("tags")]
        public string[] Tags { get; set; } = [];
    }
}
