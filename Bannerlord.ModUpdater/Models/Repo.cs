using System.Diagnostics;

namespace Bannerlord.ModUpdater.Models
{
    [DebuggerDisplay("{Name,nq}")]
    public class Repo()
    {
        public string Owner { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public ulong WorkshopId { get; set; }

        public string[] Tags { get; set; } = [];

        public string ForcedVersion { get; set; } = string.Empty;

        public ReleaseType Release { get; set; }
    }
}
