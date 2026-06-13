using Obrasci.Models;

namespace Obrasci.Services.Functional
{
    
    public static class PhotoFunctional
    {
        // 1) Higher-order function — filter by predicate.
        public static IEnumerable<Photo> FilterBy(
            IEnumerable<Photo> photos, Func<Photo, bool> predicate) =>
            photos.Where(predicate);

        // 2) Pure projection — transforms data without mutating source.
        public static IEnumerable<string> ToFileNames(IEnumerable<Photo> photos) =>
            photos.Select(p => p.FileName).ToList();

        // 3) Pure aggregation — total bytes uploaded by a user.
        public static long TotalBytes(IEnumerable<Photo> photos) =>
            photos.Aggregate(0L, (sum, p) => sum + p.SizeBytes);

        // 4) Function composition — combine two predicates.
        public static Func<Photo, bool> And(
            Func<Photo, bool> a, Func<Photo, bool> b) =>
            p => a(p) && b(p);

        // 5) Pure parser — extract hashtags into immutable list.
        public static IReadOnlyList<string> ParseHashtags(string? raw) =>
            string.IsNullOrWhiteSpace(raw)
                ? Array.Empty<string>()
                : raw.Split(new[] { ',', ' ', '#' },
                            StringSplitOptions.RemoveEmptyEntries
                          | StringSplitOptions.TrimEntries)
                     .Select(t => t.ToLowerInvariant())
                     .Distinct()
                     .ToList();

        // 6) Curried pricing/limit calculator (pure).
        public static Func<PackageType, int> DailyLimitFor =>
            pkg => PackageLimits.GetDailyLimit(pkg);

        // 7) Group-by reduction — counts per author (immutable result).
        public static IReadOnlyDictionary<string, int> CountByAuthor(
            IEnumerable<Photo> photos) =>
            photos.GroupBy(p => p.User?.UserName ?? "unknown")
                  .ToDictionary(g => g.Key, g => g.Count());
    }
}
