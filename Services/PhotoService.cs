using Microsoft.EntityFrameworkCore;
using Obrasci.Data;
using Obrasci.Metrics;
using Obrasci.Models;
using Obrasci.Services.Functional;
using Obrasci.Services.ImageProcessing;
using Obrasci.Services.Storage;

namespace Obrasci.Services
{
  
    public class PhotoService : IPhotoService
    {
        private readonly ApplicationDbContext _ctx;
        private readonly ILoggingService _logging;
        private readonly IEnumerable<IImageProcessingStrategy> _strategies;
        private readonly IPhotoStorage _storage;
        private readonly IPackageLimitService _limits;
        private readonly IAppMetrics _metrics;

        public PhotoService(ApplicationDbContext ctx,
                            ILoggingService logging,
                            IEnumerable<IImageProcessingStrategy> strategies,
                            IPhotoStorage storage,
                            IPackageLimitService limits,
                            IAppMetrics metrics)
        {
            _ctx = ctx;
            _logging = logging;
            _strategies = strategies;
            _storage = storage;
            _limits = limits;
            _metrics = metrics;
        }

        public async Task<Photo> UploadAsync(ApplicationUser user, IFormFile file,
            string? description, string? hashtags, string? processingOption)
        {
            _limits.EnforceDailyLimit(user);

            var strategy = GetStrategy(processingOption);

            using var ms = new MemoryStream();
            await using (var input = file.OpenReadStream())
                await strategy.ProcessAsync(input, ms, file.ContentType ?? "image/jpeg");
            ms.Position = 0;

            var relativePath = await _storage.SaveAsync(ms, file.FileName);

            var photo = new Photo
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                FileName = file.FileName,
                StoragePath = relativePath,
                SizeBytes = ms.Length,
                UploadedAt = DateTime.UtcNow,
                Description = description,
                Hashtags = string.Join(",", PhotoFunctional.ParseHashtags(hashtags)),
                ContentType = "image/jpeg"
            };

            _ctx.Photos.Add(photo);
            user.DailyUploadCount++;
            _ctx.Update(user);
            await _ctx.SaveChangesAsync();

            _metrics.IncrementUpload(user.Package.ToString());
            _metrics.RecordUploadSize(photo.SizeBytes);

            await _logging.LogAsync(user.Id, user.Email,
                $"Uploaded photo {photo.Id} ({file.FileName}) with {strategy.Name}");

            return photo;
        }

        public async Task<IEnumerable<Photo>> GetLastAsync(int count) =>
            await _ctx.Photos.Include(p => p.User)
                .OrderByDescending(p => p.UploadedAt).Take(count).ToListAsync();

        public async Task<Photo?> GetByIdAsync(Guid id) =>
            await _ctx.Photos.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == id);

        public async Task<IEnumerable<Photo>> SearchAsync(string? hashtag, long? minSize, long? maxSize,
            DateTime? from, DateTime? to, string? authorUserName)
        {
            var query = _ctx.Photos.Include(p => p.User).AsQueryable();

            if (!string.IsNullOrWhiteSpace(hashtag))
            {
                var tag = hashtag.Trim();
                query = query.Where(p => p.Hashtags != null && p.Hashtags.Contains(tag));
            }
            if (minSize.HasValue) query = query.Where(p => p.SizeBytes >= minSize.Value);
            if (maxSize.HasValue) query = query.Where(p => p.SizeBytes <= maxSize.Value);
            if (from.HasValue) query = query.Where(p => p.UploadedAt >= from.Value);
            if (to.HasValue) query = query.Where(p => p.UploadedAt <= to.Value);
            if (!string.IsNullOrWhiteSpace(authorUserName))
            {
                var author = authorUserName.Trim();
                query = query.Where(p => p.User.UserName == author);
            }
            return await query.OrderByDescending(p => p.UploadedAt).ToListAsync();
        }

        public async Task<(Photo photo, byte[] fileBytes)> GetFileAsync(Guid id)
        {
            var photo = await GetByIdAsync(id) ?? throw new FileNotFoundException("Photo not found.");
            var bytes = await _storage.ReadAsync(photo.StoragePath);
            return (photo, bytes);
        }

        public async Task<(Photo photo, byte[] fileBytes)> GetProcessedFileAsync(Guid id, string processingOption)
        {
            var photo = await GetByIdAsync(id) ?? throw new FileNotFoundException("Photo not found.");
            var sourceBytes = await _storage.ReadAsync(photo.StoragePath);

            var strategy = GetStrategy(processingOption);

            await using var input = new MemoryStream(sourceBytes);
            await using var ms = new MemoryStream();
            await strategy.ProcessAsync(input, ms, photo.ContentType ?? "image/jpeg");

            await _logging.LogAsync(photo.UserId, photo.User?.Email,
                $"Downloaded processed photo {photo.Id} with {strategy.Name}");

            return (photo, ms.ToArray());
        }

        private IImageProcessingStrategy GetStrategy(string? name) =>
            string.IsNullOrWhiteSpace(name)
                ? _strategies.First(s => s.Name == "Original")
                : _strategies.FirstOrDefault(s => s.Name == name)
                    ?? _strategies.First(s => s.Name == "Original");
    }
}
