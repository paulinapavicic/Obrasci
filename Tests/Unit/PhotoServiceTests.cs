using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using Obrasci.Data;
using Obrasci.Metrics;
using Obrasci.Models;
using Obrasci.Services;
using Obrasci.Services.ImageProcessing;
using Obrasci.Services.Storage;
using Xunit;

namespace Tests.Unit
{
    public class PhotoServiceTests : IDisposable
    {
        private readonly ApplicationDbContext _context;

        private readonly Mock<ILoggingService> _logging = new();
        private readonly Mock<IPhotoStorage> _storage = new();
        private readonly Mock<IPackageLimitService> _limits = new();
        private readonly Mock<IAppMetrics> _metrics = new();

        private readonly TestImageProcessingStrategy _originalStrategy =
            new("Original");

        private readonly TestImageProcessingStrategy _grayscaleStrategy =
            new("Grayscale");

        private readonly PhotoService _service;

        public PhotoServiceTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);

            _service = new PhotoService(
                _context,
                _logging.Object,
                new IImageProcessingStrategy[]
                {
                    _originalStrategy,
                    _grayscaleStrategy
                },
                _storage.Object,
                _limits.Object,
                _metrics.Object);
        }

        public void Dispose()
        {
            _context.Dispose();
        }

        [Fact]
        public async Task UploadAsync_with_valid_file_saves_photo_and_records_side_effects()
        {
          
            var user = CreateUser();

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var file = CreateFormFile(
                "photo.jpg",
                "image/jpeg",
                new byte[] { 1, 2, 3, 4 });

            _storage
                .Setup(s => s.SaveAsync(It.IsAny<Stream>(), "photo.jpg"))
                .ReturnsAsync("uploads/photo.jpg");

        
            var result = await _service.UploadAsync(
                user,
                file,
                "My image",
                "#summer #holiday",
                "Original");

         
            result.UserId.Should().Be(user.Id);
            result.FileName.Should().Be("photo.jpg");
            result.StoragePath.Should().Be("uploads/photo.jpg");
            result.Description.Should().Be("My image");
            result.Hashtags.Should().NotBeNullOrWhiteSpace();
            result.ContentType.Should().Be("image/jpeg");
            result.SizeBytes.Should().BeGreaterThan(0);
            result.UploadedAt.Should().BeCloseTo(
                DateTime.UtcNow,
                TimeSpan.FromSeconds(5));

            var saved = await _context.Photos.SingleAsync(p => p.Id == result.Id);

            saved.UserId.Should().Be(user.Id);
            saved.FileName.Should().Be("photo.jpg");
            saved.StoragePath.Should().Be("uploads/photo.jpg");

            user.DailyUploadCount.Should().Be(1);

            _limits.Verify(
                l => l.EnforceDailyLimit(user),
                Times.Once);

            _storage.Verify(
                s => s.SaveAsync(It.IsAny<Stream>(), "photo.jpg"),
                Times.Once);

            _metrics.Verify(
                m => m.IncrementUpload(user.Package.ToString()),
                Times.Once);

            _metrics.Verify(
                m => m.RecordUploadSize(result.SizeBytes),
                Times.Once);

            _logging.Verify(
                l => l.LogAsync(
                    user.Id,
                    user.Email,
                    It.Is<string>(message =>
                        message.Contains("Uploaded photo") &&
                        message.Contains("Original"))),
                Times.Once);

            _originalStrategy.CallCount.Should().Be(1);
        }

        [Fact]
        public async Task UploadAsync_with_null_description_and_hashtags_saves_photo()
        {
            
            var user = CreateUser();

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var file = CreateFormFile(
                "photo.jpg",
                "image/jpeg",
                new byte[] { 1, 2, 3 });

            _storage
                .Setup(s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>()))
                .ReturnsAsync("uploads/photo.jpg");

       
            var result = await _service.UploadAsync(
                user,
                file,
                null,
                null,
                null);

           
            result.Description.Should().BeNull();
            result.Hashtags.Should().BeEmpty();
            result.FileName.Should().Be("photo.jpg");

            _originalStrategy.CallCount.Should().Be(1);
        }

        [Fact]
        public async Task UploadAsync_when_daily_limit_throws_does_not_save_photo()
        {
          
            var user = CreateUser();
            var file = CreateFormFile(
                "photo.jpg",
                "image/jpeg",
                new byte[] { 1, 2, 3 });

            _limits
                .Setup(l => l.EnforceDailyLimit(user))
                .Throws(new InvalidOperationException(
                    "Daily upload limit reached for your package."));

           
            Func<Task> act = () => _service.UploadAsync(
                user,
                file,
                null,
                null,
                "Original");

      
            await act.Should()
                .ThrowAsync<InvalidOperationException>()
                .WithMessage("*Daily upload limit*");

            (await _context.Photos.CountAsync()).Should().Be(0);

            _storage.Verify(
                s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>()),
                Times.Never);

            _metrics.Verify(
                m => m.IncrementUpload(It.IsAny<string>()),
                Times.Never);

            _logging.Verify(
                l => l.LogAsync(
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task GetLastAsync_when_there_are_no_photos_returns_empty_collection()
        {
       
            var result = await _service.GetLastAsync(10);

         
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetLastAsync_returns_newest_photos_first_and_applies_limit()
        {
          
            var user = CreateUser();

            var oldest = CreatePhoto(
                user,
                DateTime.UtcNow.AddMinutes(-3));

            var middle = CreatePhoto(
                user,
                DateTime.UtcNow.AddMinutes(-2));

            var newest = CreatePhoto(
                user,
                DateTime.UtcNow.AddMinutes(-1));

            _context.Photos.AddRange(oldest, middle, newest);
            await _context.SaveChangesAsync();

         
            var result = (await _service.GetLastAsync(2)).ToList();

            
            result.Should().HaveCount(2);
            result.Select(p => p.Id).Should()
                .ContainInOrder(newest.Id, middle.Id);

            result.Should().NotContain(p => p.Id == oldest.Id);
        }

        [Fact]
        public async Task GetByIdAsync_when_photo_exists_returns_photo_with_user()
        {
            
            var user = CreateUser();
            var photo = CreatePhoto(user, DateTime.UtcNow);

            _context.Photos.Add(photo);
            await _context.SaveChangesAsync();

        
            var result = await _service.GetByIdAsync(photo.Id);

          
            result.Should().NotBeNull();
            result!.Id.Should().Be(photo.Id);
            result.User.Should().NotBeNull();
            result.User!.Id.Should().Be(user.Id);
        }

        [Fact]
        public async Task GetByIdAsync_when_photo_does_not_exist_returns_null()
        {
          
            var result = await _service.GetByIdAsync(Guid.NewGuid());

            
            result.Should().BeNull();
        }

        [Fact]
        public async Task SearchAsync_without_filters_returns_all_photos_newest_first()
        {
          
            var user = CreateUser("author");

            var oldPhoto = CreatePhoto(
                user,
                DateTime.UtcNow.AddMinutes(-2));

            var newPhoto = CreatePhoto(
                user,
                DateTime.UtcNow.AddMinutes(-1));

            _context.Photos.AddRange(oldPhoto, newPhoto);
            await _context.SaveChangesAsync();

            
            var result = (await _service.SearchAsync(
                null,
                null,
                null,
                null,
                null,
                null)).ToList();

           
            result.Select(p => p.Id).Should()
                .ContainInOrder(newPhoto.Id, oldPhoto.Id);
        }

        [Fact]
        public async Task SearchAsync_with_hashtag_trims_input_and_returns_matching_photo()
        {
            
            var user = CreateUser();

            var matching = CreatePhoto(
                user,
                DateTime.UtcNow,
                hashtags: "summer,travel");

            var notMatching = CreatePhoto(
                user,
                DateTime.UtcNow.AddMinutes(-1),
                hashtags: "work");

            _context.Photos.AddRange(matching, notMatching);
            await _context.SaveChangesAsync();

          
            var result = (await _service.SearchAsync(
                "  summer  ",
                null,
                null,
                null,
                null,
                null)).ToList();

           
            result.Should().ContainSingle();
            result[0].Id.Should().Be(matching.Id);
        }

        [Fact]
        public async Task SearchAsync_with_size_date_and_author_filters_returns_only_matching_photo()
        {
          
            var alice = CreateUser("alice");
            var bob = CreateUser("bob");

            var matching = CreatePhoto(
                alice,
                DateTime.UtcNow.AddHours(-1),
                sizeBytes: 500,
                hashtags: "travel");

            var wrongSize = CreatePhoto(
                alice,
                DateTime.UtcNow.AddHours(-1),
                sizeBytes: 50);

            var wrongAuthor = CreatePhoto(
                bob,
                DateTime.UtcNow.AddHours(-1),
                sizeBytes: 500);

            var wrongDate = CreatePhoto(
                alice,
                DateTime.UtcNow.AddDays(-10),
                sizeBytes: 500);

            _context.Photos.AddRange(
                matching,
                wrongSize,
                wrongAuthor,
                wrongDate);

            await _context.SaveChangesAsync();

            var from = DateTime.UtcNow.AddDays(-2);
            var to = DateTime.UtcNow;

          
            var result = (await _service.SearchAsync(
                hashtag: null,
                minSize: 100,
                maxSize: 1000,
                from: from,
                to: to,
                authorUserName: "  alice  ")).ToList();

            result.Should().ContainSingle();
            result[0].Id.Should().Be(matching.Id);
        }

        [Fact]
        public async Task GetFileAsync_when_photo_exists_reads_stored_file()
        {
           
            var user = CreateUser();
            var photo = CreatePhoto(
                user,
                DateTime.UtcNow,
                storagePath: "uploads/a.jpg");

            var expectedBytes = new byte[] { 10, 20, 30 };

            _context.Photos.Add(photo);
            await _context.SaveChangesAsync();

            _storage
                .Setup(s => s.ReadAsync("uploads/a.jpg"))
                .ReturnsAsync(expectedBytes);

       
            var (returnedPhoto, bytes) = await _service.GetFileAsync(photo.Id);

          
            returnedPhoto.Id.Should().Be(photo.Id);
            bytes.Should().Equal(expectedBytes);

            _storage.Verify(
                s => s.ReadAsync("uploads/a.jpg"),
                Times.Once);
        }

        [Fact]
        public async Task GetFileAsync_when_photo_does_not_exist_throws_file_not_found()
        {
          
            Func<Task> act = () => _service.GetFileAsync(Guid.NewGuid());

     
            await act.Should()
                .ThrowAsync<FileNotFoundException>()
                .WithMessage("Photo not found.");
        }

        [Fact]
        public async Task GetProcessedFileAsync_with_known_strategy_processes_file_and_logs_download()
        {
            
            var user = CreateUser();
            var photo = CreatePhoto(
                user,
                DateTime.UtcNow,
                storagePath: "uploads/a.jpg");

            var sourceBytes = new byte[] { 1, 2, 3 };

            _context.Photos.Add(photo);
            await _context.SaveChangesAsync();

            _storage
                .Setup(s => s.ReadAsync("uploads/a.jpg"))
                .ReturnsAsync(sourceBytes);

            
            var (returnedPhoto, bytes) = await _service.GetProcessedFileAsync(
                photo.Id,
                "Grayscale");

        
            returnedPhoto.Id.Should().Be(photo.Id);
            bytes.Should().Equal(sourceBytes);

            _grayscaleStrategy.CallCount.Should().Be(1);

            _logging.Verify(
                l => l.LogAsync(
                    photo.UserId,
                    user.Email,
                    It.Is<string>(message =>
                        message.Contains("Downloaded processed photo") &&
                        message.Contains("Grayscale"))),
                Times.Once);
        }

        [Theory]
        [InlineData("")]
        [InlineData("Unknown strategy")]
        public async Task GetProcessedFileAsync_with_blank_or_unknown_option_uses_original_strategy(
            string processingOption)
        {
            
            var user = CreateUser();
            var photo = CreatePhoto(
                user,
                DateTime.UtcNow,
                storagePath: "uploads/a.jpg");

            _context.Photos.Add(photo);
            await _context.SaveChangesAsync();

            _storage
                .Setup(s => s.ReadAsync("uploads/a.jpg"))
                .ReturnsAsync(new byte[] { 1, 2, 3 });

          
            await _service.GetProcessedFileAsync(photo.Id, processingOption);

         
            _originalStrategy.CallCount.Should().Be(1);
        }

        [Fact]
        public async Task GetProcessedFileAsync_when_photo_does_not_exist_throws_file_not_found()
        {
            
            Func<Task> act = () => _service.GetProcessedFileAsync(
                Guid.NewGuid(),
                "Original");

           
            await act.Should()
                .ThrowAsync<FileNotFoundException>()
                .WithMessage("Photo not found.");
        }

        private static ApplicationUser CreateUser(string userName = "test-user")
        {
            return new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = userName,
                Email = $"{userName}@example.test",
                Package = PackageType.Free
            };
        }

        private static Photo CreatePhoto(
            ApplicationUser user,
            DateTime uploadedAt,
            string storagePath = "uploads/photo.jpg",
            long sizeBytes = 100,
            string? hashtags = null)
        {
            return new Photo
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                User = user,
                FileName = "photo.jpg",
                StoragePath = storagePath,
                SizeBytes = sizeBytes,
                UploadedAt = uploadedAt,
                ContentType = "image/jpeg",
                Hashtags = hashtags
            };
        }

        private static IFormFile CreateFormFile(
            string fileName,
            string contentType,
            byte[] bytes)
        {
            var stream = new MemoryStream(bytes);

            return new FormFile(
                stream,
                0,
                stream.Length,
                "file",
                fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = contentType
            };
        }

        private sealed class TestImageProcessingStrategy : IImageProcessingStrategy
        {
            public TestImageProcessingStrategy(string name)
            {
                Name = name;
            }

            public string Name { get; }

            public int CallCount { get; private set; }

            public async Task ProcessAsync(
                Stream input,
                Stream output,
                string contentType)
            {
                CallCount++;

                await input.CopyToAsync(output);
            }
        }
        [Fact]
        public async Task UploadAsync_with_known_processing_option_uses_matching_strategy()
        {
            // Arrange
            var user = CreateUser();

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var file = CreateFormFile(
                "photo.jpg",
                "image/jpeg",
                new byte[] { 1, 2, 3, 4 });

            _storage
                .Setup(s => s.SaveAsync(It.IsAny<Stream>(), "photo.jpg"))
                .ReturnsAsync("uploads/photo.jpg");

            // Act
            var result = await _service.UploadAsync(
                user,
                file,
                "Grayscale upload",
                "#test",
                "Grayscale");

            // Assert
            result.Should().NotBeNull();

            _grayscaleStrategy.CallCount.Should().Be(1);
            _originalStrategy.CallCount.Should().Be(0);

            _logging.Verify(
                l => l.LogAsync(
                    user.Id,
                    user.Email,
                    It.Is<string>(message =>
                        message.Contains("Uploaded photo") &&
                        message.Contains("Grayscale"))),
                Times.Once);
        }
        [Fact]
        public async Task UploadAsync_with_unknown_processing_option_uses_original_strategy()
        {
            // Arrange
            var user = CreateUser();

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var file = CreateFormFile(
                "photo.jpg",
                "image/jpeg",
                new byte[] { 1, 2, 3, 4 });

            _storage
                .Setup(s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>()))
                .ReturnsAsync("uploads/photo.jpg");

            // Act
            await _service.UploadAsync(
                user,
                file,
                null,
                null,
                "NotARealStrategy");

            // Assert
            _originalStrategy.CallCount.Should().Be(1);
            _grayscaleStrategy.CallCount.Should().Be(0);
        }
        [Fact]
        public async Task UploadAsync_when_storage_save_fails_does_not_persist_photo_or_record_metrics()
        {
            // Arrange
            var user = CreateUser();

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var file = CreateFormFile(
                "photo.jpg",
                "image/jpeg",
                new byte[] { 1, 2, 3, 4 });

            _storage
                .Setup(s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>()))
                .ThrowsAsync(new IOException("Storage is unavailable."));

            // Act
            Func<Task> act = () => _service.UploadAsync(
                user,
                file,
                null,
                null,
                "Original");

            // Assert
            await act.Should()
                .ThrowAsync<IOException>()
                .WithMessage("Storage is unavailable.");

            (await _context.Photos.CountAsync()).Should().Be(0);

            user.DailyUploadCount.Should().Be(0);

            _metrics.Verify(
                m => m.IncrementUpload(It.IsAny<string>()),
                Times.Never);

            _metrics.Verify(
                m => m.RecordUploadSize(It.IsAny<long>()),
                Times.Never);

            _logging.Verify(
                l => l.LogAsync(
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<string>()),
                Times.Never);
        }
        [Fact]
        public async Task GetLastAsync_with_zero_count_returns_empty_collection()
        {
            // Arrange
            var user = CreateUser();

            var photo = CreatePhoto(
                user,
                DateTime.UtcNow);

            _context.Photos.Add(photo);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetLastAsync(0);

            // Assert
            result.Should().BeEmpty();
        }
        [Fact]
        public async Task SearchAsync_with_whitespace_hashtag_and_author_does_not_apply_those_filters()
        {
            // Arrange
            var firstUser = CreateUser("first-author");
            var secondUser = CreateUser("second-author");

            var firstPhoto = CreatePhoto(
                firstUser,
                DateTime.UtcNow.AddMinutes(-2),
                hashtags: "summer");

            var secondPhoto = CreatePhoto(
                secondUser,
                DateTime.UtcNow.AddMinutes(-1),
                hashtags: "winter");

            _context.Photos.AddRange(firstPhoto, secondPhoto);
            await _context.SaveChangesAsync();

            // Act
            var result = (await _service.SearchAsync(
                hashtag: "   ",
                minSize: null,
                maxSize: null,
                from: null,
                to: null,
                authorUserName: "   "))
                .ToList();

            // Assert
            result.Should().HaveCount(2);

            result.Select(photo => photo.Id)
                .Should()
                .Contain(new[] { firstPhoto.Id, secondPhoto.Id });
        }
        [Fact]
        public async Task SearchAsync_includes_photos_at_minimum_and_maximum_size_boundaries()
        {
            // Arrange
            var user = CreateUser();

            var minPhoto = CreatePhoto(
                user,
                DateTime.UtcNow.AddMinutes(-2),
                sizeBytes: 100);

            var middlePhoto = CreatePhoto(
                user,
                DateTime.UtcNow.AddMinutes(-1),
                sizeBytes: 150);

            var maxPhoto = CreatePhoto(
                user,
                DateTime.UtcNow,
                sizeBytes: 200);

            var tooSmall = CreatePhoto(
                user,
                DateTime.UtcNow.AddMinutes(-3),
                sizeBytes: 99);

            var tooLarge = CreatePhoto(
                user,
                DateTime.UtcNow.AddMinutes(-4),
                sizeBytes: 201);

            _context.Photos.AddRange(
                minPhoto,
                middlePhoto,
                maxPhoto,
                tooSmall,
                tooLarge);

            await _context.SaveChangesAsync();

            // Act
            var result = (await _service.SearchAsync(
                hashtag: null,
                minSize: 100,
                maxSize: 200,
                from: null,
                to: null,
                authorUserName: null))
                .ToList();

            // Assert
            result.Select(photo => photo.Id)
    .Should()
    .BeEquivalentTo(new[]
    {
        minPhoto.Id,
        middlePhoto.Id,
        maxPhoto.Id
    });
        }
        [Fact]
        public async Task SearchAsync_includes_photos_at_from_and_to_date_boundaries()
        {
            // Arrange
            var user = CreateUser();

            var from = DateTime.UtcNow.AddDays(-2);
            var to = DateTime.UtcNow.AddDays(-1);

            var atFrom = CreatePhoto(
                user,
                from,
                sizeBytes: 100);

            var atTo = CreatePhoto(
                user,
                to,
                sizeBytes: 100);

            var before = CreatePhoto(
                user,
                from.AddTicks(-1),
                sizeBytes: 100);

            var after = CreatePhoto(
                user,
                to.AddTicks(1),
                sizeBytes: 100);

            _context.Photos.AddRange(
                atFrom,
                atTo,
                before,
                after);

            await _context.SaveChangesAsync();

            // Act
            var result = (await _service.SearchAsync(
                hashtag: null,
                minSize: null,
                maxSize: null,
                from: from,
                to: to,
                authorUserName: null))
                .ToList();

            // Assert
           result.Select(photo => photo.Id)
    .Should()
    .BeEquivalentTo(new[]
    {
        atFrom.Id,
        atTo.Id
    });
        }
        [Fact]
        public async Task GetFileAsync_when_storage_read_fails_propagates_exception()
        {
            // Arrange
            var user = CreateUser();

            var photo = CreatePhoto(
                user,
                DateTime.UtcNow,
                storagePath: "uploads/missing.jpg");

            _context.Photos.Add(photo);
            await _context.SaveChangesAsync();

            _storage
                .Setup(s => s.ReadAsync("uploads/missing.jpg"))
                .ThrowsAsync(new IOException("Storage read failed."));

            // Act
            Func<Task> act = () => _service.GetFileAsync(photo.Id);

            // Assert
            await act.Should()
                .ThrowAsync<IOException>()
                .WithMessage("Storage read failed.");
        }
        [Fact]
        public async Task GetProcessedFileAsync_when_storage_read_fails_does_not_log_download()
        {
            // Arrange
            var user = CreateUser();

            var photo = CreatePhoto(
                user,
                DateTime.UtcNow,
                storagePath: "uploads/missing.jpg");

            _context.Photos.Add(photo);
            await _context.SaveChangesAsync();

            _storage
                .Setup(s => s.ReadAsync("uploads/missing.jpg"))
                .ThrowsAsync(new IOException("Storage read failed."));

            // Act
            Func<Task> act = () => _service.GetProcessedFileAsync(
                photo.Id,
                "Original");

            // Assert
            await act.Should()
                .ThrowAsync<IOException>()
                .WithMessage("Storage read failed.");

            _logging.Verify(
                l => l.LogAsync(
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<string>()),
                Times.Never);
        }
    }
}