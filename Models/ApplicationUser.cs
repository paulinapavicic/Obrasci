using Microsoft.AspNetCore.Identity;

namespace Obrasci.Models
{
    public class ApplicationUser : IdentityUser
    {
        public PackageType Package { get; set; } = PackageType.Free;
        public int DailyUploadCount { get; set; }
        public DateTime? LastUploadDate { get; set; }
        public DateTime? PackageLastChanged { get; set; }

        public PackageType? PendingPackage { get; set; }
        public DateTime? PendingPackageEffectiveDate { get; set; }
    }

}
