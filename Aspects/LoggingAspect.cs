using System.Reflection;
using Microsoft.Extensions.Logging;

namespace Obrasci.Aspects
{
  
    public class LoggingAspect<T> : DispatchProxy where T : class
    {
        private T _decorated = default!;
        private ILogger _logger = default!;

        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method == null) return null;
            var name = $"{typeof(T).Name}.{method.Name}";
            _logger.LogInformation("[Aspect:Logging] -> {Name} args={Args}",
                name, string.Join(",", (args ?? Array.Empty<object>()).Select(a => a?.ToString() ?? "null")));
            try
            {
                var result = method.Invoke(_decorated, args);

                if (result is Task t)
                {
                    t.ContinueWith(task =>
                    {
                        if (task.IsFaulted)
                            _logger.LogError(task.Exception?.GetBaseException(), "[Aspect:Logging] !! {Name} FAILED", name);
                        else
                            _logger.LogInformation("[Aspect:Logging] <- {Name} OK", name);
                    });

                    return result;
                }

                _logger.LogInformation("[Aspect:Logging] <- {Name} OK", name);
                return result;
            }
            catch (TargetInvocationException ex)
            {
                _logger.LogError(ex.InnerException, "[Aspect:Logging] !! {Name} FAILED", name);
                throw ex.InnerException ?? ex;
            }
        }

        public static T Create(T decorated, ILogger logger)
        {
            object proxy = Create<T, LoggingAspect<T>>()!;
            ((LoggingAspect<T>)proxy)._decorated = decorated;
            ((LoggingAspect<T>)proxy)._logger = logger;
            return (T)proxy;
        }
    }
}
