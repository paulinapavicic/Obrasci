using Obrasci.Models;

namespace Obrasci.ViewModels
{
    public class AdminUserStatsViewModel
    {
        public ApplicationUser User { get; set; } = default!;
        public int TotalPhotos { get; set; }
        public long TotalSizeBytes { get; set; }
    }
}
