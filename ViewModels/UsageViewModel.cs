using Obrasci.Models;

namespace Obrasci.ViewModels
{
    public class UsageViewModel
    {
        public PackageType Package { get; set; }
        public int DailyUploadCount { get; set; }
        public int DailyUploadLimit { get; set; }
        public int TotalPhotos { get; set; }
        public long TotalSizeBytes { get; set; }
    }

}
