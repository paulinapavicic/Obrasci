using FluentAssertions;
using Obrasci.Metrics;
using Xunit;

namespace Tests.Unit
{
    public class AppMetricsTests
    {
        [Fact]
        public void Snapshot_contains_custom_photos_per_minute_metric()
        {
            var m = new AppMetrics();
            m.IncrementUpload("Free");
            m.IncrementUpload("Pro");
            var snap = m.Snapshot();
            snap.Should().ContainKey("photos_per_minute_rate");
            snap["photos_per_minute_rate"].Should().BeGreaterThanOrEqualTo(2);
        }

        [Fact]
        public void Counters_increment_correctly()
        {
            var m = new AppMetrics();
            m.IncrementHttpRequest("/photos");
            m.IncrementHttpRequest("/photos");
            m.Snapshot()["http_requests_total"].Should().Be(2);
        }
    }
}
