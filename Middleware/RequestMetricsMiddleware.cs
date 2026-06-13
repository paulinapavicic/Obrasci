using Obrasci.Metrics;

namespace Obrasci.Middleware
{
  
    public class RequestMetricsMiddleware
    {
        private readonly RequestDelegate _next;
        public RequestMetricsMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext context, IAppMetrics metrics)
        {
            metrics.IncrementHttpRequest(context.Request.Path);
            await _next(context);
        }
    }
}
