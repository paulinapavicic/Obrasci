using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Moq;
using Obrasci.Services.Storage;
using Xunit;

namespace Tests.Unit
{
    public class LocalFileSystemPhotoStorageTests : IDisposable
    {
        private readonly string _webRootPath;
        private readonly LocalFileSystemPhotoStorage _storage;

        public LocalFileSystemPhotoStorageTests()
        {
            _webRootPath = Path.Combine(
                Path.GetTempPath(),
                "ObrasciStorageTests",
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(_webRootPath);

            var environment = new Mock<IWebHostEnvironment>();

            environment
                .SetupGet(item => item.WebRootPath)
                .Returns(_webRootPath);

            _storage = new LocalFileSystemPhotoStorage(environment.Object);
        }

        public void Dispose()
        {
            if (Directory.Exists(_webRootPath))
            {
                Directory.Delete(
                    _webRootPath,
                    recursive: true);
            }
        }

        [Fact]
        public async Task SaveAsync_creates_uploads_directory_and_saves_file_contents()
        {
          
            var expectedBytes = new byte[] { 10, 20, 30, 40 };

            await using var content = new MemoryStream(expectedBytes);

            
            var relativePath = await _storage.SaveAsync(
                content,
                "photo.jpg");

         
            relativePath.Should().StartWith("uploads/");
            relativePath.Should().EndWith("_photo.jpg");

            var physicalPath = ToPhysicalPath(relativePath);

            File.Exists(physicalPath).Should().BeTrue();

            var savedBytes = await File.ReadAllBytesAsync(physicalPath);

            savedBytes.Should().Equal(expectedBytes);
        }

        [Fact]
        public async Task SaveAsync_with_same_original_filename_generates_unique_paths()
        {
           
            await using var firstContent = new MemoryStream(
                new byte[] { 1, 2, 3 });

            await using var secondContent = new MemoryStream(
                new byte[] { 4, 5, 6 });

          
            var firstPath = await _storage.SaveAsync(
                firstContent,
                "same-name.jpg");

            var secondPath = await _storage.SaveAsync(
                secondContent,
                "same-name.jpg");

           
            firstPath.Should().NotBe(secondPath);

            File.Exists(ToPhysicalPath(firstPath)).Should().BeTrue();
            File.Exists(ToPhysicalPath(secondPath)).Should().BeTrue();

            (await File.ReadAllBytesAsync(ToPhysicalPath(firstPath)))
                .Should()
                .Equal(1, 2, 3);

            (await File.ReadAllBytesAsync(ToPhysicalPath(secondPath)))
                .Should()
                .Equal(4, 5, 6);
        }

        [Fact]
        public async Task SaveAsync_uses_only_filename_and_does_not_preserve_path_segments()
        {
            
            await using var content = new MemoryStream(
                new byte[] { 1, 2, 3 });

            var suppliedFileName = Path.Combine(
                "some-folder",
                "nested-folder",
                "photo.jpg");

            
            var relativePath = await _storage.SaveAsync(
                content,
                suppliedFileName);

            relativePath.Should().StartWith("uploads/");
            relativePath.Should().EndWith("_photo.jpg");

            relativePath.Should().NotContain("some-folder");
            relativePath.Should().NotContain("nested-folder");

            File.Exists(ToPhysicalPath(relativePath)).Should().BeTrue();
        }

        [Fact]
        public async Task ReadAsync_when_file_exists_returns_its_bytes()
        {
          
            var expectedBytes = new byte[] { 50, 60, 70 };

            var relativePath = "uploads/test-file.jpg";
            var physicalPath = ToPhysicalPath(relativePath);

            Directory.CreateDirectory(
                Path.GetDirectoryName(physicalPath)!);

            await File.WriteAllBytesAsync(
                physicalPath,
                expectedBytes);

            
            var result = await _storage.ReadAsync(relativePath);

           
            result.Should().Equal(expectedBytes);
        }

        [Fact]
        public async Task ReadAsync_when_file_does_not_exist_throws_file_not_found()
        {
            
            const string relativePath = "uploads/missing.jpg";

            
            Func<Task> act = () => _storage.ReadAsync(relativePath);

           
            await act.Should()
                .ThrowAsync<FileNotFoundException>()
                .WithMessage("File not found.");
        }

        [Fact]
        public void Exists_when_file_exists_returns_true()
        {
            const string relativePath = "uploads/existing.jpg";

            var physicalPath = ToPhysicalPath(relativePath);

            Directory.CreateDirectory(
                Path.GetDirectoryName(physicalPath)!);

            File.WriteAllBytes(
                physicalPath,
                new byte[] { 1 });

            
            var exists = _storage.Exists(relativePath);

            
            exists.Should().BeTrue();
        }

        [Fact]
        public void Exists_when_file_does_not_exist_returns_false()
        {
            
            var exists = _storage.Exists("uploads/does-not-exist.jpg");
            exists.Should().BeFalse();
        }

        [Fact]
        public async Task SaveAsync_with_empty_stream_creates_empty_file()
        {
            
            await using var content = new MemoryStream();

         
            var relativePath = await _storage.SaveAsync(
                content,
                "empty.jpg");

            
            var physicalPath = ToPhysicalPath(relativePath);

            File.Exists(physicalPath).Should().BeTrue();

            var bytes = await File.ReadAllBytesAsync(physicalPath);

            bytes.Should().BeEmpty();
        }

        private string ToPhysicalPath(string relativePath)
        {
            return Path.Combine(
                _webRootPath,
                relativePath.Replace(
                    "/",
                    Path.DirectorySeparatorChar.ToString()));
        }
    }
}