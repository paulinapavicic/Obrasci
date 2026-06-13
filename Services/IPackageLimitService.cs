using Obrasci.Models;

namespace Obrasci.Services
{
    
    public interface IPackageLimitService
    {
        int GetDailyLimit(PackageType package);
        void EnforceDailyLimit(ApplicationUser user);
    }

    public class PackageLimitService : IPackageLimitService
    {
        public int GetDailyLimit(PackageType package) =>
            PackageLimits.GetDailyLimit(package);

        public void EnforceDailyLimit(ApplicationUser user)
        {
            var today = DateTime.UtcNow.Date;
            if (user.LastUploadDate?.Date != today)
            {
                user.LastUploadDate = today;
                user.DailyUploadCount = 0;
            }

            var maxPerDay = GetDailyLimit(user.Package);
            if (user.DailyUploadCount >= maxPerDay)
                throw new InvalidOperationException(
                    "Daily upload limit reached for your package.");
        }
    }
}
