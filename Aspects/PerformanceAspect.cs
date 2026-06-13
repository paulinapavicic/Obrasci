using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Obrasci.Metrics;

namespace Obrasci.Aspects
{
   
    public class PerformanceAspect<T> : DispatchProxy where T : class
    {
        private T _decorated = default!;
        private ILogger _logger = default!;
        private IAppMetrics _metrics = default!;

        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method == null) return null;
            var name = $"{typeof(T).Name}.{method.Name}";
            var sw = Stopwatch.StartNew();
            try
            {
                var result = method.Invoke(_decorated, args);
                    if (result is Task t)
                    {
                        t.ContinueWith(task =>
                        {
                            sw.Stop();

                            if (task.IsFaulted)
                            {
                                _metrics.IncrementMethodFailure(name);
                                _logger.LogError(task.Exception?.GetBaseException(),
                                    "[Aspect:Perf] {Name} FAILED after {Ms} ms", name, sw.ElapsedMilliseconds);
                            }
                            else
                            {
                                _logger.LogInformation("[Aspect:Perf] {Name} took {Ms} ms", name, sw.ElapsedMilliseconds);
                            }

                            _metrics.RecordMethodDuration(name, sw.ElapsedMilliseconds);
                        });

                        return result;
                    }
                
                else
                {
                    sw.Stop();
                    _metrics.RecordMethodDuration(name, sw.ElapsedMilliseconds);
                    _logger.LogInformation("[Aspect:Perf] {Name} took {Ms} ms", name, sw.ElapsedMilliseconds);
                }
                return result;
            }
            catch (TargetInvocationException ex)
            {
                sw.Stop();
                _metrics.IncrementMethodFailure(name);
                throw ex.InnerException ?? ex;
            }
        }

        public static T Create(T decorated, ILogger logger, IAppMetrics metrics)
        {
            object proxy = Create<T, PerformanceAspect<T>>()!;
            var p = (PerformanceAspect<T>)proxy;
            p._decorated = decorated;
            p._logger = logger;
            p._metrics = metrics;
            return (T)proxy;
        }
    }
}
