using FluentAssertions;
using Obrasci.Models;
using Obrasci.Services;
using Xunit;

namespace Tests.Unit
{
    
    public class PackageLimitServiceTests
    {
        private readonly PackageLimitService _svc = new();

        [Theory]
        [InlineData(PackageType.Free, 5)]
        [InlineData(PackageType.Pro, 20)]
        [InlineData(PackageType.Gold, 100)]
        public void GetDailyLimit_returns_correct_quota_per_package(PackageType pkg, int expected) =>
            _svc.GetDailyLimit(pkg).Should().Be(expected);

        [Fact]
        public void EnforceDailyLimit_resets_counter_on_new_day()
        {
            var user = new ApplicationUser
            {
                Package = PackageType.Free,
                DailyUploadCount = 99,
                LastUploadDate = DateTime.UtcNow.AddDays(-1)
            };
            _svc.EnforceDailyLimit(user);
            user.DailyUploadCount.Should().Be(0);
        }

        [Fact]
        public void EnforceDailyLimit_throws_when_quota_exceeded()
        {
            var user = new ApplicationUser
            {
                Package = PackageType.Free,
                DailyUploadCount = 5,
                LastUploadDate = DateTime.UtcNow.Date
            };
            var act = () => _svc.EnforceDailyLimit(user);
            act.Should().Throw<InvalidOperationException>()
               .WithMessage("*Daily upload limit*");
        }

        [Fact]
        public void EnforceDailyLimit_passes_when_under_quota()
        {
            var user = new ApplicationUser
            {
                Package = PackageType.Pro,
                DailyUploadCount = 1,
                LastUploadDate = DateTime.UtcNow.Date
            };
            var act = () => _svc.EnforceDailyLimit(user);
            act.Should().NotThrow();
        }
        [Fact]
        public void EnforceDailyLimit_does_not_reset_counter_on_same_day()
        {
            var user = new ApplicationUser
            {
                Package = PackageType.Free,
                DailyUploadCount = 3,
                LastUploadDate = DateTime.UtcNow.Date
            };

            _svc.EnforceDailyLimit(user);

            user.DailyUploadCount.Should().Be(3);
        }
        [Fact]
        public void EnforceDailyLimit_allows_free_user_one_upload_below_limit()
        {
            var user = new ApplicationUser
            {
                Package = PackageType.Free,
                DailyUploadCount = 4,
                LastUploadDate = DateTime.UtcNow.Date
            };

            var act = () => _svc.EnforceDailyLimit(user);

            act.Should().NotThrow();
        }
        [Fact]
        public void EnforceDailyLimit_rejects_pro_user_at_daily_limit()
        {
            var user = new ApplicationUser
            {
                Package = PackageType.Pro,
                DailyUploadCount = 20,
                LastUploadDate = DateTime.UtcNow.Date
            };

            var act = () => _svc.EnforceDailyLimit(user);

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*Daily upload limit*");
        }
        [Fact]
        public void EnforceDailyLimit_allows_pro_user_one_upload_below_daily_limit()
        {
            var user = new ApplicationUser
            {
                Package = PackageType.Pro,
                DailyUploadCount = 19,
                LastUploadDate = DateTime.UtcNow.Date
            };

            var act = () => _svc.EnforceDailyLimit(user);

            act.Should().NotThrow();
        }
        [Fact]
        public void EnforceDailyLimit_rejects_gold_user_at_daily_limit()
        {
            var user = new ApplicationUser
            {
                Package = PackageType.Gold,
                DailyUploadCount = 100,
                LastUploadDate = DateTime.UtcNow.Date
            };

            var act = () => _svc.EnforceDailyLimit(user);

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*Daily upload limit*");
        }
        [Fact]
        public void EnforceDailyLimit_allows_gold_user_one_upload_below_daily_limit()
        {
            var user = new ApplicationUser
            {
                Package = PackageType.Gold,
                DailyUploadCount = 99,
                LastUploadDate = DateTime.UtcNow.Date
            };

            var act = () => _svc.EnforceDailyLimit(user);

            act.Should().NotThrow();
        }

    }
}
