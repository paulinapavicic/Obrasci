using Microsoft.EntityFrameworkCore;
using Obrasci.Data;
using Obrasci.Models;
using Obrasci.Services.ImageProcessing;

namespace Obrasci.Services
{
   
    public class PhotoService : IPhotoService
    {
        private readonly ApplicationDbContext _ctx;
        private readonly IWebHostEnvironment _env;
        private readonly ILoggingService _logging;
        private readonly IEnumerable<IImageProcessingStrategy> _strategies;

        public PhotoService(ApplicationDbContext ctx, IWebHostEnvironment env, ILoggingService logging, IEnumerable<IImageProcessingStrategy> strategies)
        {
            _ctx = ctx;
            _env = env;
            _logging = logging;
            _strategies = strategies;
        }

        public async Task<Photo> UploadAsync(ApplicationUser user, IFormFile file,
                                       string? description, string? hashtags,
                                       string? processingOption)
        {
            EnforcePackageLimit(user);

            var uploadsRoot = Path.Combine(_env.WebRootPath, "uploads");
            Directory.CreateDirectory(uploadsRoot);

            var uniqueName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            var physicalPath = Path.Combine(uploadsRoot, uniqueName);

            var strategy = GetStrategy(processingOption);

            await using (var input = file.OpenReadStream())
            await using (var output = new FileStream(physicalPath, FileMode.Create))
            {
                await strategy.ProcessAsync(input, output, file.ContentType ?? "image/jpeg");
            }

            var photo = new Photo
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                FileName = file.FileName,
                StoragePath = Path.Combine("uploads", uniqueName).Replace("\\", "/"),
                SizeBytes = new FileInfo(physicalPath).Length,
                UploadedAt = DateTime.UtcNow,
                Description = description,
                Hashtags = hashtags,
                ContentType = "image/jpeg"
            };

            _ctx.Photos.Add(photo);
            user.DailyUploadCount++;
            _ctx.Update(user);
            await _ctx.SaveChangesAsync();

            await _logging.LogAsync(
     user.Id,
     user.Email,
     $"Uploaded photo {photo.Id} ({file.FileName}) with {strategy.Name}");


            return photo;
        }

        public async Task<IEnumerable<Photo>> GetLastAsync(int count)
        {
            return await _ctx.Photos
                .Include(p => p.User)
                .OrderByDescending(p => p.UploadedAt)
                .Take(count)
                .ToListAsync();
        }


        private void EnforcePackageLimit(ApplicationUser user)
        {
            var today = DateTime.UtcNow.Date;

        
            if (user.LastUploadDate?.Date != today)
            {
                user.LastUploadDate = today;
                user.DailyUploadCount = 0;
            }

            var maxPerDay = user.Package switch
            {
                PackageType.Free => 5,
                PackageType.Pro => 20,
                PackageType.Gold => 100,
                _ => 5
            };

            if (user.DailyUploadCount >= maxPerDay)
            {
                throw new InvalidOperationException("Daily upload limit reached for your package.");
            }
        }


        public async Task<Photo?> GetByIdAsync(Guid id)
        {
            return await _ctx.Photos
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<IEnumerable<Photo>> SearchAsync(
    string? hashtag, long? minSize, long? maxSize,
    DateTime? from, DateTime? to, string? authorUserName)
        {
            var query = _ctx.Photos
                .Include(p => p.User)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(hashtag))
            {
                var tag = hashtag.Trim();
                query = query.Where(p => p.Hashtags != null && p.Hashtags.Contains(tag));
            }

            if (minSize.HasValue)
                query = query.Where(p => p.SizeBytes >= minSize.Value);

            if (maxSize.HasValue)
                query = query.Where(p => p.SizeBytes <= maxSize.Value);

            if (from.HasValue)
                query = query.Where(p => p.UploadedAt >= from.Value);

            if (to.HasValue)
                query = query.Where(p => p.UploadedAt <= to.Value);

            if (!string.IsNullOrWhiteSpace(authorUserName))
            {
                var author = authorUserName.Trim();
                query = query.Where(p => p.User.UserName == author);
            }

            return await query
                .OrderByDescending(p => p.UploadedAt)
                .ToListAsync();
        }

        public async Task<(Photo photo, byte[] fileBytes)> GetFileAsync(Guid id)
        {
            var photo = await GetByIdAsync(id);
            if (photo == null)
                throw new FileNotFoundException("Photo not found.");

            var physicalPath = Path.Combine(_env.WebRootPath, photo.StoragePath.Replace("/", Path.DirectorySeparatorChar.ToString()));

            if (!File.Exists(physicalPath))
                throw new FileNotFoundException("File not found on disk.", physicalPath);

            var bytes = await File.ReadAllBytesAsync(physicalPath);
            return (photo, bytes);
        }


        private IImageProcessingStrategy GetStrategy(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return _strategies.First(s => s.Name == "Original");

            return _strategies.FirstOrDefault(s => s.Name == name)
                   ?? _strategies.First(s => s.Name == "Original");
        }

        public async Task<(Photo photo, byte[] fileBytes)> GetProcessedFileAsync(Guid id, string processingOption)
        {
            var photo = await GetByIdAsync(id);
            if (photo == null)
                throw new FileNotFoundException("Photo not found.");

            var originalPath = Path.Combine(_env.WebRootPath,
                photo.StoragePath.Replace("/", Path.DirectorySeparatorChar.ToString()));

            if (!File.Exists(originalPath))
                throw new FileNotFoundException("File not found on disk.", originalPath);

            var strategy = GetStrategy(processingOption);

            await using var input = new FileStream(originalPath, FileMode.Open, FileAccess.Read);
            await using var ms = new MemoryStream();
            await strategy.ProcessAsync(input, ms, photo.ContentType ?? "image/jpeg");
            var bytes = ms.ToArray();

            await _logging.LogAsync(
    photo.UserId,
    photo.User?.Email,   
    $"Downloaded processed photo {photo.Id} with {strategy.Name}");


            return (photo, bytes);
        }


    }
}
