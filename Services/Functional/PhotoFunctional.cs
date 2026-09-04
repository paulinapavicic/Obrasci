using Obrasci.Models;

namespace Obrasci.Services.Functional
{
    public static class PhotoFunctional
    {

        public static IEnumerable<Photo> FilterBy(
            IEnumerable<Photo> photos, Func<Photo, bool> predicate) =>
            photos.Where(predicate);

        
        public static IEnumerable<string> ToFileNames(IEnumerable<Photo> photos) =>
            photos.Select(p => p.FileName).ToList();

        public static long TotalBytes(IEnumerable<Photo> photos) =>
            photos.Aggregate(0L, (sum, p) => sum + p.SizeBytes);

        
        public static Func<Photo, bool> And(
            Func<Photo, bool> a, Func<Photo, bool> b) =>
            p => a(p) && b(p);

        
        public static IReadOnlyList<string> ParseHashtags(string? raw) =>
            string.IsNullOrWhiteSpace(raw)
                ? Array.Empty<string>()
                : raw.Split(new[] { ',', ' ', '#' },
                            StringSplitOptions.RemoveEmptyEntries
                          | StringSplitOptions.TrimEntries)
                     .Select(t => t.ToLowerInvariant())
                     .Distinct()
                     .ToList();

     
        public static Func<PackageType, int> DailyLimitFor =>
            pkg => PackageLimits.GetDailyLimit(pkg);

        public static IReadOnlyDictionary<string, int> CountByAuthor(
            IEnumerable<Photo> photos) =>
            photos.GroupBy(p => p.User?.UserName ?? "unknown")
                  .ToDictionary(g => g.Key, g => g.Count());
    }
}
