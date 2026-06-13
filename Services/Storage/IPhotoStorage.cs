namespace Obrasci.Services.Storage
{
  
    public interface IPhotoStorage
    {
        Task<string> SaveAsync(Stream content, string fileName);
        Task<byte[]> ReadAsync(string relativePath);
        bool Exists(string relativePath);
    }

    public class LocalFileSystemPhotoStorage : IPhotoStorage
    {
        private readonly IWebHostEnvironment _env;
        public LocalFileSystemPhotoStorage(IWebHostEnvironment env) => _env = env;

        public async Task<string> SaveAsync(Stream content, string fileName)
        {
            var uploadsRoot = Path.Combine(_env.WebRootPath, "uploads");
            Directory.CreateDirectory(uploadsRoot);

            var unique = $"{Guid.NewGuid()}_{Path.GetFileName(fileName)}";
            var physicalPath = Path.Combine(uploadsRoot, unique);

            await using var fs = new FileStream(physicalPath, FileMode.Create);
            await content.CopyToAsync(fs);

            return Path.Combine("uploads", unique).Replace("\\", "/");
        }

        public Task<byte[]> ReadAsync(string relativePath)
        {
            var physical = Path.Combine(_env.WebRootPath,
                relativePath.Replace("/", Path.DirectorySeparatorChar.ToString()));
            if (!File.Exists(physical))
                throw new FileNotFoundException("File not found.", physical);
            return File.ReadAllBytesAsync(physical);
        }

        public bool Exists(string relativePath)
        {
            var physical = Path.Combine(_env.WebRootPath,
                relativePath.Replace("/", Path.DirectorySeparatorChar.ToString()));
            return File.Exists(physical);
        }
    }
}
