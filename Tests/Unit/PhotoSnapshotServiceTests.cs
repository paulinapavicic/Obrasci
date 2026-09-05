using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Moq;
using Obrasci.Data;
using Obrasci.Models;
using Obrasci.Services;
using Xunit;

namespace Tests.Unit
{
    public class PhotoSnapshotServiceTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly string _contentRootPath;
        private readonly PhotoSnapshotService _service;

        public PhotoSnapshotServiceTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);

            _contentRootPath = Path.Combine(
                Path.GetTempPath(),
                "ObrasciSnapshotTests",
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(_contentRootPath);

            var environment = new Mock<IWebHostEnvironment>();

            environment
                .SetupGet(item => item.ContentRootPath)
                .Returns(_contentRootPath);

            _service = new PhotoSnapshotService(
                _context,
                environment.Object);
        }

        public void Dispose()
        {
            _context.Dispose();

            if (Directory.Exists(_contentRootPath))
            {
                Directory.Delete(
                    _contentRootPath,
                    recursive: true);
            }
        }

        [Fact]
        public async Task ExportPhotoAsync_when_photo_does_not_exist_returns_null_and_creates_no_export()
        {
            
            var result = await _service.ExportPhotoAsync(Guid.NewGuid());

           
            result.Should().BeNull();

            var exportDirectory = Path.Combine(
                _contentRootPath,
                "App_Data",
                "serialized");

            Directory.Exists(exportDirectory).Should().BeFalse();
        }

        [Fact]
        public async Task ExportPhotoAsync_when_photo_exists_creates_json_snapshot_with_expected_values()
        {
            
            var photo = new Photo
            {
                Id = Guid.NewGuid(),
                UserId = "user-123",
                FileName = "sunset.jpg",
                StoragePath = "uploads/ignored-for-snapshot.jpg",
                Description = "A sunset at the beach",
                Hashtags = "sunset,beach",
                ContentType = "image/jpeg",
                SizeBytes = 12_345,
                UploadedAt = DateTime.SpecifyKind(
                    new DateTime(2026, 9, 4, 10, 30, 0),
                    DateTimeKind.Utc)
            };

            _context.Photos.Add(photo);
            await _context.SaveChangesAsync();

            
            var fileName = await _service.ExportPhotoAsync(photo.Id);

           
            fileName.Should().NotBeNullOrWhiteSpace();
            fileName.Should().StartWith($"photo-{photo.Id:N}-");
            fileName.Should().EndWith(".json");

            var exportPath = Path.Combine(
                _contentRootPath,
                "App_Data",
                "serialized",
                fileName!);

            File.Exists(exportPath).Should().BeTrue();

            var json = await File.ReadAllTextAsync(exportPath);

            json.Should().Contain("\"PhotoId\"");
            json.Should().Contain("\"FileName\"");
            json.Should().Contain("\"ExportedAtUtc\"");

            await using var stream = File.OpenRead(exportPath);

            var snapshot = await JsonSerializer.DeserializeAsync<PhotoExportSnapshot>(
                stream);

            snapshot.Should().NotBeNull();

            snapshot!.PhotoId.Should().Be(photo.Id);
            snapshot.FileName.Should().Be("sunset.jpg");
            snapshot.Description.Should().Be("A sunset at the beach");
            snapshot.Hashtags.Should().Be("sunset,beach");
            snapshot.ContentType.Should().Be("image/jpeg");
            snapshot.SizeBytes.Should().Be(12_345);

            snapshot.UploadedAtUtc.Should().Be(
                DateTime.SpecifyKind(
                    new DateTime(2026, 9, 4, 10, 30, 0),
                    DateTimeKind.Utc));

            snapshot.ExportedAtUtc.Should().BeCloseTo(
                DateTime.UtcNow,
                TimeSpan.FromSeconds(5));
        }

        [Fact]
        public async Task ExportPhotoAsync_creates_serialized_directory_when_it_does_not_exist()
        {
            
            var photo = CreatePhoto();

            _context.Photos.Add(photo);
            await _context.SaveChangesAsync();

            var exportDirectory = Path.Combine(
                _contentRootPath,
                "App_Data",
                "serialized");

            Directory.Exists(exportDirectory).Should().BeFalse();

           
            var fileName = await _service.ExportPhotoAsync(photo.Id);

         
            fileName.Should().NotBeNullOrWhiteSpace();
            Directory.Exists(exportDirectory).Should().BeTrue();

            File.Exists(Path.Combine(exportDirectory, fileName!))
                .Should()
                .BeTrue();
        }

        [Fact]
        public async Task ExportPhotoAsync_creates_distinct_snapshot_files_for_multiple_exports()
        {
            
            var firstPhoto = CreatePhoto(fileName: "first.jpg");
            var secondPhoto = CreatePhoto(fileName: "second.jpg");

            _context.Photos.AddRange(firstPhoto, secondPhoto);
            await _context.SaveChangesAsync();

           
            var firstExport = await _service.ExportPhotoAsync(firstPhoto.Id);
            var secondExport = await _service.ExportPhotoAsync(secondPhoto.Id);

          
            firstExport.Should().NotBeNullOrWhiteSpace();
            secondExport.Should().NotBeNullOrWhiteSpace();
            firstExport.Should().NotBe(secondExport);

            var exportDirectory = Path.Combine(
                _contentRootPath,
                "App_Data",
                "serialized");

            File.Exists(Path.Combine(exportDirectory, firstExport!))
                .Should()
                .BeTrue();

            File.Exists(Path.Combine(exportDirectory, secondExport!))
                .Should()
                .BeTrue();
        }

        [Fact]
        public async Task ExportPhotoAsync_with_null_optional_metadata_writes_null_values_to_snapshot()
        {
            
            var photo = CreatePhoto(
                fileName: "minimal.jpg",
                description: null,
                hashtags: null,
                contentType: null);

            _context.Photos.Add(photo);
            await _context.SaveChangesAsync();
  var fileName = await _service.ExportPhotoAsync(photo.Id);

            var exportPath = Path.Combine(
                _contentRootPath,
                "App_Data",
                "serialized",
                fileName!);

            await using var stream = File.OpenRead(exportPath);

            var snapshot = await JsonSerializer.DeserializeAsync<PhotoExportSnapshot>(
                stream);

           
            snapshot.Should().NotBeNull();
            snapshot!.Description.Should().BeNull();
            snapshot.Hashtags.Should().BeNull();
            snapshot.ContentType.Should().BeNull();
        }

        private static Photo CreatePhoto(
            string fileName = "photo.jpg",
            string? description = "Test photo",
            string? hashtags = "test,photo",
            string? contentType = "image/jpeg")
        {
            return new Photo
            {
                Id = Guid.NewGuid(),
                UserId = "user-123",
                FileName = fileName,
                StoragePath = $"uploads/{fileName}",
                Description = description,
                Hashtags = hashtags,
                ContentType = contentType,
                SizeBytes = 100,
                UploadedAt = DateTime.UtcNow.AddMinutes(-1)
            };
        }
    }
}