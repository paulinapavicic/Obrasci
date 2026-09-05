using System.Security.Claims;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Obrasci.Data;
using Obrasci.Services;
using Xunit;

namespace Tests.Unit
{
    public class ActionLoggerTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly ActionLogger _logger;

        public ActionLoggerTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);
            _logger = new ActionLogger(_context);
        }

        public void Dispose()
        {
            _context.Dispose();
        }

        [Fact]
        public async Task LogAsync_with_user_id_and_name_claim_saves_complete_log()
        {
            
            var principal = CreatePrincipal(
                userId: "user-123",
                name: "pauli@example.test");

            var before = DateTime.UtcNow;

           
            await _logger.LogAsync(
                principal,
                "Downloaded processed photo");

            var after = DateTime.UtcNow;

            
            var log = await _context.UserActionLogs.SingleAsync();

            log.UserId.Should().Be("user-123");
            log.UserEmail.Should().Be("pauli@example.test");
            log.Action.Should().Be("Downloaded processed photo");
            log.Timestamp.Should().BeOnOrAfter(before);
            log.Timestamp.Should().BeOnOrBefore(after);
        }

        [Fact]
        public async Task LogAsync_without_name_identifier_claim_saves_null_user_id()
        {
            
            var principal = CreatePrincipal(
                userId: null,
                name: "anonymous-name@example.test");

            await _logger.LogAsync(
                principal,
                "Viewed public photo list");

            
            var log = await _context.UserActionLogs.SingleAsync();

            log.UserId.Should().BeNull();
            log.UserEmail.Should().Be("anonymous-name@example.test");
            log.Action.Should().Be("Viewed public photo list");
        }

        [Fact]
        public async Task LogAsync_without_name_claim_saves_null_user_email()
        {
            
            var principal = CreatePrincipal(
                userId: "user-456",
                name: null);

            
            await _logger.LogAsync(
                principal,
                "Deleted photo");

            
            var log = await _context.UserActionLogs.SingleAsync();

            log.UserId.Should().Be("user-456");
            log.UserEmail.Should().BeNull();
            log.Action.Should().Be("Deleted photo");
        }

        [Fact]
        public async Task LogAsync_with_empty_principal_saves_log_without_user_details()
        {
            
            var principal = new ClaimsPrincipal();

            
            await _logger.LogAsync(
                principal,
                "Anonymous action");

            
            var log = await _context.UserActionLogs.SingleAsync();

            log.UserId.Should().BeNull();
            log.UserEmail.Should().BeNull();
            log.Action.Should().Be("Anonymous action");
            log.Timestamp.Kind.Should().Be(DateTimeKind.Utc);
        }

        [Fact]
        public async Task LogAsync_multiple_calls_persist_separate_entries()
        {
           
            var firstUser = CreatePrincipal(
                userId: "user-1",
                name: "one@example.test");

            var secondUser = CreatePrincipal(
                userId: "user-2",
                name: "two@example.test");

            await _logger.LogAsync(
                firstUser,
                "First action");

            await _logger.LogAsync(
                secondUser,
                "Second action");

            var logs = await _context.UserActionLogs
                .OrderBy(log => log.Action)
                .ToListAsync();

            logs.Should().HaveCount(2);

            logs.Select(log => log.Action)
                .Should()
                .Equal("First action", "Second action");

            logs.Select(log => log.UserId)
                .Should()
                .Equal("user-1", "user-2");

            logs.Select(log => log.UserEmail)
                .Should()
                .Equal("one@example.test", "two@example.test");
        }

        private static ClaimsPrincipal CreatePrincipal(
            string? userId,
            string? name)
        {
            var claims = new List<Claim>();

            if (userId is not null)
            {
                claims.Add(
                    new Claim(
                        ClaimTypes.NameIdentifier,
                        userId));
            }

            if (name is not null)
            {
                claims.Add(
                    new Claim(
                        ClaimTypes.Name,
                        name));
            }

            var identity = new ClaimsIdentity(
                claims,
                authenticationType: "TestAuthentication");

            return new ClaimsPrincipal(identity);
        }
    }
}