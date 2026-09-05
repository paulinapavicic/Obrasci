using System.Diagnostics;
using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Obrasci.Controllers;
using Obrasci.Data;
using Obrasci.Models;
using Xunit;

namespace Tests.Unit
{
    public class HomeControllerTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly HomeController _controller;

        public HomeControllerTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);

            _controller = new HomeController(
                Mock.Of<ILogger<HomeController>>(),
                _context);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            _controller.HttpContext.TraceIdentifier = "test-trace-id";
        }

        public void Dispose()
        {
            _context.Dispose();
        }

        [Fact]
        public void Privacy_returns_view()
        {
            
            var result = _controller.Privacy();

            
            result.Should().BeOfType<ViewResult>();
        }

        [Fact]
        public void Error_when_no_current_activity_uses_http_trace_identifier()
        {
            
            _controller.HttpContext.TraceIdentifier = "trace-id-123";

         
            var result = _controller.Error();

            
            var view = result.Should().BeOfType<ViewResult>().Subject;

            var model = view.Model
                .Should()
                .BeOfType<ErrorViewModel>()
                .Subject;

            model.RequestId.Should().Be("trace-id-123");
            model.ShowRequestId.Should().BeTrue();
        }

        [Fact]
        public void Error_when_activity_exists_uses_activity_id()
        {
           
            using var activity = new Activity("HomeControllerTest");
            activity.Start();

           
            var result = _controller.Error();

          
            var view = result.Should().BeOfType<ViewResult>().Subject;

            var model = view.Model
                .Should()
                .BeOfType<ErrorViewModel>()
                .Subject;

            model.RequestId.Should().Be(activity.Id);
            model.ShowRequestId.Should().BeTrue();
        }

        [Fact]
        public void Error_has_no_store_response_cache_attribute()
        {
            
            var method = typeof(HomeController).GetMethod(
                nameof(HomeController.Error));

            var attribute = method!
                .GetCustomAttribute<ResponseCacheAttribute>();

           
            attribute.Should().NotBeNull();
            attribute!.Duration.Should().Be(0);
            attribute.Location.Should().Be(ResponseCacheLocation.None);
            attribute.NoStore.Should().BeTrue();
        }

        [Fact]
        public async Task Index_when_no_photos_exist_sets_empty_latest_photos()
        {
            
            var result = await _controller.Index();

            result.Should().BeOfType<ViewResult>();

            var latestPhotos = _controller.ViewData["LatestPhotos"]
                .Should()
                .BeAssignableTo<IEnumerable<Photo>>()
                .Subject
                .ToList();

            latestPhotos.Should().BeEmpty();
        }

        [Fact]
        public async Task Index_returns_eight_newest_photos_in_descending_upload_order()
        {
            
            var photos = Enumerable.Range(1, 10)
                .Select(index => new Photo
                {
                    Id = Guid.NewGuid(),
                    UserId = $"user-{index}",
                    FileName = $"photo-{index}.jpg",
                    StoragePath = $"uploads/photo-{index}.jpg",
                    SizeBytes = index * 100,
                    UploadedAt = DateTime.UtcNow.AddMinutes(-index),
                    ContentType = "image/jpeg"
                })
                .ToList();

            _context.Photos.AddRange(photos);
            await _context.SaveChangesAsync();

            var expected = photos
                .OrderByDescending(photo => photo.UploadedAt)
                .Take(8)
                .Select(photo => photo.Id)
                .ToList();

           
            var result = await _controller.Index();

            
            result.Should().BeOfType<ViewResult>();

            var latestPhotos = _controller.ViewData["LatestPhotos"]
                .Should()
                .BeAssignableTo<IEnumerable<Photo>>()
                .Subject
                .ToList();

            latestPhotos.Should().HaveCount(8);

            latestPhotos.Select(photo => photo.Id)
                .Should()
                .Equal(expected);
        }
    }
}