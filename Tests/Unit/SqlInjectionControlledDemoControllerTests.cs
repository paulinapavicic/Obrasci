using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Obrasci.Controllers;
using Obrasci.Data;
using Obrasci.Models;
using System.Reflection;
using Xunit;

namespace Tests.Unit
{
    public class SqlInjectionControlledDemoControllerTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly SqlInjectionControlledDemoController _controller;

        public SqlInjectionControlledDemoControllerTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);

            _controller = new SqlInjectionControlledDemoController(_context);
        }

        public void Dispose()
        {
            _context.Dispose();
        }

        [Fact]
        public void Controller_has_expected_api_route()
        {
            
            var route = typeof(SqlInjectionControlledDemoController)
                .GetCustomAttribute<RouteAttribute>();

            route.Should().NotBeNull();
            route!.Template.Should().Be("api/sql-injection-demo");
        }

        [Fact]
        public void SafeSearch_has_expected_http_get_route()
        {
            
            var method = typeof(SqlInjectionControlledDemoController)
                .GetMethod(nameof(
                    SqlInjectionControlledDemoController.SafeSearch));

            var route = method!
                .GetCustomAttribute<HttpGetAttribute>();

            route.Should().NotBeNull();
            route!.Template.Should().Be("safe-search");
        }

        [Fact]
        public void SafeSearch_binds_term_from_query_string()
        {
         
            var parameter = typeof(SqlInjectionControlledDemoController)
                .GetMethod(nameof(
                    SqlInjectionControlledDemoController.SafeSearch))!
                .GetParameters()
                .Single();

            var attribute = parameter
                .GetCustomAttribute<FromQueryAttribute>();

      
            attribute.Should().NotBeNull();
        }

        [Fact]
        public async Task SafeSearch_when_term_is_null_uses_empty_string_and_returns_all_photos()
        {
           
            var first = CreatePhoto(
                fileName: "first.jpg",
                uploadedAt: DateTime.UtcNow.AddMinutes(-5));

            var second = CreatePhoto(
                fileName: "second.jpg",
                uploadedAt: DateTime.UtcNow.AddMinutes(-1));

            _context.Photos.AddRange(first, second);
            await _context.SaveChangesAsync();


            var result = await _controller.SafeSearch(null);


            var ok = result.Should()
                .BeOfType<OkObjectResult>()
                .Subject;

            GetProperty(ok.Value!, "searchedTerm")
                .Should()
                .Be(string.Empty);

            GetProperty(ok.Value!, "resultCount")
                .Should()
                .Be(2);

            var results = GetEnumerableProperty(
                ok.Value!,
                "results");

            results.Should().HaveCount(2);
        }

        [Fact]
        public async Task SafeSearch_when_term_is_empty_returns_all_photos()
        {
          
            _context.Photos.AddRange(
                CreatePhoto(
                    fileName: "one.jpg",
                    uploadedAt: DateTime.UtcNow.AddMinutes(-2)),
                CreatePhoto(
                    fileName: "two.jpg",
                    uploadedAt: DateTime.UtcNow.AddMinutes(-1)));

            await _context.SaveChangesAsync();

    
            var result = await _controller.SafeSearch(string.Empty);

     
            var ok = result.Should()
                .BeOfType<OkObjectResult>()
                .Subject;

            GetProperty(ok.Value!, "searchedTerm")
                .Should()
                .Be(string.Empty);

            GetProperty(ok.Value!, "resultCount")
                .Should()
                .Be(2);
        }

        [Fact]
        public async Task SafeSearch_when_term_has_more_than_100_characters_returns_bad_request()
        {
          
            var term = new string('a', 101);


            var result = await _controller.SafeSearch(term);

   
            var badRequest = result.Should()
                .BeOfType<BadRequestObjectResult>()
                .Subject;

            badRequest.StatusCode.Should()
                .Be(StatusCodes.Status400BadRequest);

            GetProperty(badRequest.Value!, "message")
                .Should()
                .Be("Search term must be 100 characters or fewer.");
        }

        [Fact]
        public async Task SafeSearch_when_term_has_exactly_100_characters_does_not_return_bad_request()
        {
          
            var term = new string('a', 100);

            _context.Photos.Add(CreatePhoto(
                fileName: $"{term}.jpg",
                uploadedAt: DateTime.UtcNow));

            await _context.SaveChangesAsync();

      
            var result = await _controller.SafeSearch(term);

    
            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task SafeSearch_matches_file_name()
        {
        
            var expected = CreatePhoto(
                fileName: "mountain-sunrise.jpg",
                description: null,
                hashtags: null,
                uploadedAt: DateTime.UtcNow);

            var other = CreatePhoto(
                fileName: "city.jpg",
                description: "A city scene",
                hashtags: "urban",
                uploadedAt: DateTime.UtcNow.AddMinutes(-1));

            _context.Photos.AddRange(expected, other);
            await _context.SaveChangesAsync();

            var result = await _controller.SafeSearch("mountain");

            var ok = result.Should()
                .BeOfType<OkObjectResult>()
                .Subject;

            GetProperty(ok.Value!, "resultCount")
                .Should()
                .Be(1);

            var results = GetEnumerableProperty(
                ok.Value!,
                "results");

            GetGuidProperty(results.Single(), "Id")
                .Should()
                .Be(expected.Id);

            GetProperty(results.Single(), "FileName")
                .Should()
                .Be("mountain-sunrise.jpg");
        }

        [Fact]
        public async Task SafeSearch_matches_description()
        {
     
            var expected = CreatePhoto(
                fileName: "photo.jpg",
                description: "Beautiful golden sunset over the sea",
                hashtags: null,
                uploadedAt: DateTime.UtcNow);

            _context.Photos.AddRange(
                expected,
                CreatePhoto(
                    fileName: "other.jpg",
                    description: "Cloudy day",
                    hashtags: null,
                    uploadedAt: DateTime.UtcNow.AddMinutes(-1)));

            await _context.SaveChangesAsync();

  
            var result = await _controller.SafeSearch("sunset");

  
            var ok = result.Should()
                .BeOfType<OkObjectResult>()
                .Subject;

            GetProperty(ok.Value!, "resultCount")
                .Should()
                .Be(1);

            var results = GetEnumerableProperty(
                ok.Value!,
                "results");

            GetGuidProperty(results.Single(), "Id")
                .Should()
                .Be(expected.Id);
        }

        [Fact]
        public async Task SafeSearch_matches_hashtags()
        {
         
            var expected = CreatePhoto(
                fileName: "lake.jpg",
                description: null,
                hashtags: "#travel #nature #lake",
                uploadedAt: DateTime.UtcNow);

            _context.Photos.AddRange(
                expected,
                CreatePhoto(
                    fileName: "office.jpg",
                    description: null,
                    hashtags: "#work",
                    uploadedAt: DateTime.UtcNow.AddMinutes(-1)));

            await _context.SaveChangesAsync();

  
            var result = await _controller.SafeSearch("nature");


            var ok = result.Should()
                .BeOfType<OkObjectResult>()
                .Subject;

            GetProperty(ok.Value!, "resultCount")
                .Should()
                .Be(1);

            var results = GetEnumerableProperty(
                ok.Value!,
                "results");

            GetGuidProperty(results.Single(), "Id")
                .Should()
                .Be(expected.Id);
        }

        [Fact]
        public async Task SafeSearch_excludes_non_matching_photos()
        {
            
            _context.Photos.AddRange(
                CreatePhoto(
                    fileName: "forest.jpg",
                    description: "Green trees",
                    hashtags: "#nature",
                    uploadedAt: DateTime.UtcNow),
                CreatePhoto(
                    fileName: "office.jpg",
                    description: "Meeting room",
                    hashtags: "#work",
                    uploadedAt: DateTime.UtcNow.AddMinutes(-1)));

            await _context.SaveChangesAsync();

    
            var result = await _controller.SafeSearch("beach");

   
            var ok = result.Should()
                .BeOfType<OkObjectResult>()
                .Subject;

            GetProperty(ok.Value!, "searchedTerm")
                .Should()
                .Be("beach");

            GetProperty(ok.Value!, "resultCount")
                .Should()
                .Be(0);

            GetEnumerableProperty(ok.Value!, "results")
                .Should()
                .BeEmpty();
        }

        [Fact]
        public async Task SafeSearch_orders_matching_photos_by_newest_upload_first()
        {
           
            var oldest = CreatePhoto(
                fileName: "nature-oldest.jpg",
                uploadedAt: new DateTime(
                    2026, 9, 1, 10, 0, 0, DateTimeKind.Utc));

            var middle = CreatePhoto(
                fileName: "nature-middle.jpg",
                uploadedAt: new DateTime(
                    2026, 9, 2, 10, 0, 0, DateTimeKind.Utc));

            var newest = CreatePhoto(
                fileName: "nature-newest.jpg",
                uploadedAt: new DateTime(
                    2026, 9, 3, 10, 0, 0, DateTimeKind.Utc));

            _context.Photos.AddRange(oldest, middle, newest);
            await _context.SaveChangesAsync();

       
            var result = await _controller.SafeSearch("nature");

   
            var ok = result.Should()
                .BeOfType<OkObjectResult>()
                .Subject;

            var results = GetEnumerableProperty(
                ok.Value!,
                "results");

            results.Select(item => GetGuidProperty(item, "Id"))
                .Should()
                .Equal(newest.Id, middle.Id, oldest.Id);
        }

        [Fact]
        public async Task SafeSearch_returns_at_most_twenty_newest_matching_photos()
        {
          
            var photos = Enumerable.Range(1, 25)
                .Select(index => CreatePhoto(
                    fileName: $"nature-{index}.jpg",
                    uploadedAt: new DateTime(
                        2026,
                        9,
                        1,
                        0,
                        0,
                        0,
                        DateTimeKind.Utc)
                        .AddMinutes(index)))
                .ToList();

            _context.Photos.AddRange(photos);
            await _context.SaveChangesAsync();

            var expectedIds = photos
                .OrderByDescending(photo => photo.UploadedAt)
                .Take(20)
                .Select(photo => photo.Id)
                .ToList();

       
            var result = await _controller.SafeSearch("nature");

       
            var ok = result.Should()
                .BeOfType<OkObjectResult>()
                .Subject;

            GetProperty(ok.Value!, "resultCount")
                .Should()
                .Be(20);

            var results = GetEnumerableProperty(
                ok.Value!,
                "results");

            results.Should().HaveCount(20);

            results.Select(item => GetGuidProperty(item, "Id"))
                .Should()
                .Equal(expectedIds);
        }

        [Fact]
        public async Task SafeSearch_with_sql_injection_like_text_treats_it_as_search_text()
        {
            
            const string term = "' OR 1=1 --";

            var matchingPhoto = CreatePhoto(
                fileName: $"example {term}.jpg",
                uploadedAt: DateTime.UtcNow);

            var nonMatchingPhoto = CreatePhoto(
                fileName: "ordinary-photo.jpg",
                description: "Nothing suspicious",
                hashtags: "#normal",
                uploadedAt: DateTime.UtcNow.AddMinutes(-1));

            _context.Photos.AddRange(matchingPhoto, nonMatchingPhoto);
            await _context.SaveChangesAsync();

            var result = await _controller.SafeSearch(term);

            
            var ok = result.Should()
                .BeOfType<OkObjectResult>()
                .Subject;

            GetProperty(ok.Value!, "searchedTerm")
                .Should()
                .Be(term);

            GetProperty(ok.Value!, "resultCount")
                .Should()
                .Be(1);

            var results = GetEnumerableProperty(
                ok.Value!,
                "results");

            GetGuidProperty(results.Single(), "Id")
                .Should()
                .Be(matchingPhoto.Id);
        }

        private static Photo CreatePhoto(
            string fileName,
            DateTime uploadedAt,
            string? description = null,
            string? hashtags = null)
        {
            return new Photo
            {
                Id = Guid.NewGuid(),
                UserId = "test-user",
                FileName = fileName,
                StoragePath = $"uploads/{fileName}",
                ContentType = "image/jpeg",
                SizeBytes = 1_024,
                UploadedAt = uploadedAt,
                Description = description,
                Hashtags = hashtags
            };
        }

        private static object? GetProperty(
            object value,
            string propertyName)
        {
            return value.GetType()
                .GetProperty(propertyName)?
                .GetValue(value);
        }

        private static Guid GetGuidProperty(
            object value,
            string propertyName)
        {
            return (Guid)(GetProperty(value, propertyName)
                ?? throw new InvalidOperationException(
                    $"Property '{propertyName}' was missing or null."));
        }

        private static List<object> GetEnumerableProperty(
            object value,
            string propertyName)
        {
            var propertyValue = GetProperty(value, propertyName);

            if (propertyValue is not System.Collections.IEnumerable enumerable)
            {
                throw new InvalidOperationException(
                    $"Property '{propertyName}' was missing or is not enumerable.");
            }

            return enumerable.Cast<object>().ToList();
        }
    }
}