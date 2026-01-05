using Obrasci.Models;

namespace Obrasci.Services
{
    public interface IPhotoService
    {
        Task<Photo> UploadAsync(ApplicationUser user, IFormFile file,
                                string? description, string? hashtags,
                                string? processingOption);
        Task<IEnumerable<Photo>> GetLastAsync(int count);
        Task<Photo?> GetByIdAsync(Guid id);
        Task<IEnumerable<Photo>> SearchAsync(string? hashtag, long? minSize, long? maxSize,
                                             DateTime? from, DateTime? to, string? authorUserName);
        Task<(Photo photo, byte[] fileBytes)> GetFileAsync(Guid id);
        Task<(Photo photo, byte[] fileBytes)> GetProcessedFileAsync(Guid id, string processingOption);

    }

}
