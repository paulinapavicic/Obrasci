namespace Obrasci.Models
{
    public enum PackageType
    {
        Free = 0,
        Pro = 1,
        Gold = 2
    }


    public static class PackageLimits
    {
        public static int GetDailyLimit(PackageType package) =>
            package switch
            {
                PackageType.Free => 5,
                PackageType.Pro => 20,
                PackageType.Gold => 100,
                _ => 0
            };
    }

}
