using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Obrasci.Data;
using Obrasci.Services;
using Xunit;

namespace Tests.Unit
{
    public class LoggingServiceTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly LoggingService _service;

        public LoggingServiceTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);
            _service = new LoggingService(_context);
        }

        public void Dispose()
        {
            _context.Dispose();
        }

        [Fact]
        public async Task LogAsync_with_authenticated_user_saves_complete_log_entry()
        {
            
            var before = DateTime.UtcNow;

            await _service.LogAsync(
                "user-123",
                "pauli@example.test",
                "Uploaded photo abc");

            var after = DateTime.UtcNow;

            var log = await _context.UserActionLogs.SingleAsync();

            log.UserId.Should().Be("user-123");
            log.UserEmail.Should().Be("pauli@example.test");
            log.Action.Should().Be("Uploaded photo abc");
            log.Timestamp.Should().BeOnOrAfter(before);
            log.Timestamp.Should().BeOnOrBefore(after);
        }

        [Fact]
        public async Task LogAsync_with_anonymous_user_saves_log_without_user_details()
        {
            
            await _service.LogAsync(
                null,
                null,
                "Anonymous visitor viewed the home page");

            
            var log = await _context.UserActionLogs.SingleAsync();

            log.UserId.Should().BeNull();
            log.UserEmail.Should().BeNull();
            log.Action.Should().Be(
                "Anonymous visitor viewed the home page");
            log.Timestamp.Kind.Should().Be(DateTimeKind.Utc);
        }

        [Fact]
        public async Task LogAsync_multiple_calls_saves_separate_log_entries()
        {
            
            await _service.LogAsync(
                "user-1",
                "user1@example.test",
                "First action");

            await _service.LogAsync(
                "user-2",
                "user2@example.test",
                "Second action");

           
            var logs = await _context.UserActionLogs
                .OrderBy(log => log.Action)
                .ToListAsync();

            logs.Should().HaveCount(2);

            logs.Select(log => log.Action).Should()
                .ContainInOrder("First action", "Second action");

            logs.Select(log => log.UserId).Should()
                .BeEquivalentTo("user-1", "user-2");
        }
    }
}