using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace Obrasci.Metrics
{

    public interface IAppMetrics
    {
        void IncrementHttpRequest(string route);
        void IncrementUpload(string package);
        void RecordUploadSize(long bytes);
        void RecordMethodDuration(string method, long ms);
        void IncrementMethodFailure(string method);

        double GetPhotosPerMinuteRate();

        IReadOnlyDictionary<string, double> Snapshot();
    }

    public class AppMetrics : IAppMetrics
    {
        private readonly Meter _meter = new("Obrasci", "1.0.0");
        private readonly Counter<long> _httpRequests;
        private readonly Counter<long> _uploads;
        private readonly Histogram<long> _uploadSize;
        private readonly Histogram<long> _methodDuration;
        private readonly Counter<long> _methodFailures;

        private readonly ConcurrentDictionary<string, double> _values = new();
        private readonly ConcurrentQueue<DateTime> _uploadTimestamps = new();

        public AppMetrics()
        {
            _httpRequests = _meter.CreateCounter<long>("http_requests_total");
            _uploads = _meter.CreateCounter<long>("uploads_total");
            _uploadSize = _meter.CreateHistogram<long>("upload_size_bytes");
            _methodDuration = _meter.CreateHistogram<long>("method_duration_ms");
            _methodFailures = _meter.CreateCounter<long>("method_failures_total");
        }

        public void IncrementHttpRequest(string route)
        {
            _httpRequests.Add(1, new KeyValuePair<string, object?>("route", route));
            _values.AddOrUpdate("http_requests_total", 1, (_, v) => v + 1);
        }

        public void IncrementUpload(string package)
        {
            _uploads.Add(1, new KeyValuePair<string, object?>("package", package));
            _values.AddOrUpdate($"uploads_total{{package={package}}}", 1, (_, v) => v + 1);
            _uploadTimestamps.Enqueue(DateTime.UtcNow);
        }

        public void RecordUploadSize(long bytes)
        {
            _uploadSize.Record(bytes);
            _values.AddOrUpdate("upload_size_bytes_last", bytes, (_, _) => bytes);
        }

        public void RecordMethodDuration(string method, long ms)
        {
            _methodDuration.Record(ms, new KeyValuePair<string, object?>("method", method));
            _values.AddOrUpdate($"method_duration_ms{{method={method}}}", ms, (_, _) => ms);
        }

        public void IncrementMethodFailure(string method)
        {
            _methodFailures.Add(1, new KeyValuePair<string, object?>("method", method));
            _values.AddOrUpdate($"method_failures_total{{method={method}}}", 1, (_, v) => v + 1);
        }

      
        public double GetPhotosPerMinuteRate()
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-1);
            while (_uploadTimestamps.TryPeek(out var oldest) && oldest < cutoff)
                _uploadTimestamps.TryDequeue(out _);
            return _uploadTimestamps.Count;
        }

        public IReadOnlyDictionary<string, double> Snapshot()
        {
            var copy = new Dictionary<string, double>(_values)
            {
                ["photos_per_minute_rate"] = GetPhotosPerMinuteRate()
            };
            return copy;
        }
    }
}
