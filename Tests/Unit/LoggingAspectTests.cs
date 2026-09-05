using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Obrasci.Aspects;
using Xunit;

namespace Tests.Unit
{
    public class LoggingAspectTests
    {
        [Fact]
        public void Create_returns_proxy_that_implements_the_decorated_interface()
        {
          
            var logger = new Mock<ILogger>();
            ITestService decorated = new TestService();

            
            var proxy = LoggingAspect<ITestService>.Create(
                decorated,
                logger.Object);

            
            proxy.Should().NotBeNull();
            proxy.Should().BeAssignableTo<ITestService>();
            proxy.Should().NotBeSameAs(decorated);
        }

        [Fact]
        public void Synchronous_void_method_returns_normally_and_logs_started_and_success()
        {
            
            var logger = new Mock<ILogger>();
            var decorated = new TestService();

            var proxy = LoggingAspect<ITestService>.Create(
                decorated,
                logger.Object);

            proxy.Record("hello", 42, null);

            
            decorated.LastText.Should().Be("hello");
            decorated.LastNumber.Should().Be(42);
            decorated.LastOptionalValue.Should().BeNull();

            VerifyLog(
                logger,
                LogLevel.Information,
                "[Aspect:Logging] -> ITestService.Record args=hello,42,null",
                Times.Once());

            VerifyLog(
                logger,
                LogLevel.Information,
                "[Aspect:Logging] <- ITestService.Record OK",
                Times.Once());
        }

        [Fact]
        public void Synchronous_method_returns_original_value_and_logs_started_and_success()
        {
            
            var logger = new Mock<ILogger>();
            ITestService proxy = LoggingAspect<ITestService>.Create(
                new TestService(),
                logger.Object);

           
            var result = proxy.Add(7, 5);

           
            result.Should().Be(12);

            VerifyLog(
                logger,
                LogLevel.Information,
                "[Aspect:Logging] -> ITestService.Add args=7,5",
                Times.Once());

            VerifyLog(
                logger,
                LogLevel.Information,
                "[Aspect:Logging] <- ITestService.Add OK",
                Times.Once());
        }

        [Fact]
        public void Synchronous_method_when_decorated_method_throws_rethrows_inner_exception_and_logs_error()
        {
          
            var logger = new Mock<ILogger>();
            ITestService proxy = LoggingAspect<ITestService>.Create(
                new TestService(),
                logger.Object);

            
            var action = () => proxy.ThrowSynchronously();

            
            action.Should()
                .Throw<InvalidOperationException>()
                .WithMessage("Synchronous failure.");

            VerifyLog(
                logger,
                LogLevel.Information,
                "[Aspect:Logging] -> ITestService.ThrowSynchronously args=",
                Times.Once());

            VerifyLog(
                logger,
                LogLevel.Error,
                "[Aspect:Logging] !! ITestService.ThrowSynchronously FAILED",
                Times.Once(),
                expectedExceptionType: typeof(InvalidOperationException));
        }

        [Fact]
        public async Task Asynchronous_task_when_it_completes_successfully_logs_started_and_success()
        {
            
            var logger = new Mock<ILogger>();
            ITestService proxy = LoggingAspect<ITestService>.Create(
                new TestService(),
                logger.Object);

        
            await proxy.CompleteAsync();

            
            await WaitUntilAsync(() =>
                CountLogs(
                    logger,
                    LogLevel.Information,
                    "[Aspect:Logging] <- ITestService.CompleteAsync OK") == 1);

           
            VerifyLog(
                logger,
                LogLevel.Information,
                "[Aspect:Logging] -> ITestService.CompleteAsync args=",
                Times.Once());

            VerifyLog(
                logger,
                LogLevel.Information,
                "[Aspect:Logging] <- ITestService.CompleteAsync OK",
                Times.Once());
        }

        [Fact]
        public async Task Asynchronous_task_of_t_when_it_completes_successfully_returns_value_and_logs_success()
        {
            var logger = new Mock<ILogger>();
            ITestService proxy = LoggingAspect<ITestService>.Create(
                new TestService(),
                logger.Object);

            
            var result = await proxy.GetNumberAsync();

            await WaitUntilAsync(() =>
                CountLogs(
                    logger,
                    LogLevel.Information,
                    "[Aspect:Logging] <- ITestService.GetNumberAsync OK") == 1);

           
            result.Should().Be(123);

            VerifyLog(
                logger,
                LogLevel.Information,
                "[Aspect:Logging] -> ITestService.GetNumberAsync args=",
                Times.Once());

            VerifyLog(
                logger,
                LogLevel.Information,
                "[Aspect:Logging] <- ITestService.GetNumberAsync OK",
                Times.Once());
        }

        [Fact]
        public async Task Asynchronous_task_when_it_faults_preserves_exception_and_logs_error()
        {
            
            var logger = new Mock<ILogger>();
            ITestService proxy = LoggingAspect<ITestService>.Create(
                new TestService(),
                logger.Object);

            
            var action = async () => await proxy.ThrowAsynchronously();

            await action.Should()
                .ThrowAsync<InvalidOperationException>()
                .WithMessage("Asynchronous failure.");

            await WaitUntilAsync(() =>
                CountLogs(
                    logger,
                    LogLevel.Error,
                    "[Aspect:Logging] !! ITestService.ThrowAsynchronously FAILED") == 1);

            VerifyLog(
                logger,
                LogLevel.Information,
                "[Aspect:Logging] -> ITestService.ThrowAsynchronously args=",
                Times.Once());

            VerifyLog(
                logger,
                LogLevel.Error,
                "[Aspect:Logging] !! ITestService.ThrowAsynchronously FAILED",
                Times.Once(),
                expectedExceptionType: typeof(InvalidOperationException));
        }

        [Fact]
        public void Synchronous_method_with_no_arguments_logs_empty_argument_list()
        {
       
            var logger = new Mock<ILogger>();
            ITestService proxy = LoggingAspect<ITestService>.Create(
                new TestService(),
                logger.Object);

            
            proxy.NoArguments();

           
            VerifyLog(
                logger,
                LogLevel.Information,
                "[Aspect:Logging] -> ITestService.NoArguments args=",
                Times.Once());

            VerifyLog(
                logger,
                LogLevel.Information,
                "[Aspect:Logging] <- ITestService.NoArguments OK",
                Times.Once());
        }

        private static async Task WaitUntilAsync(
            Func<bool> condition,
            int timeoutMilliseconds = 2_000)
        {
            var startedAt = DateTime.UtcNow;

            while (!condition())
            {
                if ((DateTime.UtcNow - startedAt).TotalMilliseconds >
                    timeoutMilliseconds)
                {
                    throw new TimeoutException(
                        "The expected asynchronous logging continuation did not run.");
                }

                await Task.Delay(20);
            }
        }

        private static int CountLogs(
            Mock<ILogger> logger,
            LogLevel level,
            string expectedMessage)
        {
            return logger.Invocations.Count(invocation =>
            {
                if (invocation.Method.Name != nameof(ILogger.Log))
                {
                    return false;
                }

                if (invocation.Arguments.Count < 3)
                {
                    return false;
                }

                if (invocation.Arguments[0] is not LogLevel actualLevel ||
                    actualLevel != level)
                {
                    return false;
                }

                return invocation.Arguments[2]?
                    .ToString()?
                    .Contains(expectedMessage) == true;
            });
        }

        private static void VerifyLog(
     Mock<ILogger> logger,
     LogLevel expectedLevel,
     string expectedMessage,
     Times times,
     Type? expectedExceptionType = null)
        {
            var matchingLogs = logger.Invocations
                .Where(invocation => invocation.Method.Name == nameof(ILogger.Log))
                .Where(invocation => invocation.Arguments[0] is LogLevel level &&
                                     level == expectedLevel)
                .Where(invocation => invocation.Arguments[2]?
                    .ToString()?
                    .Contains(expectedMessage) == true)
                .Where(invocation =>
                {
                    if (expectedExceptionType is null)
                    {
                        return true;
                    }

                    return invocation.Arguments[3] is Exception exception &&
                           exception.GetType() == expectedExceptionType;
                })
                .ToList();

            matchingLogs.Count.Should().Be(times.ToString() == "Once" ? 1 : matchingLogs.Count);
        }

        private interface ITestService
        {
            void Record(string text, int number, object? optionalValue);

            int Add(int left, int right);

            void NoArguments();

            void ThrowSynchronously();

            Task CompleteAsync();

            Task<int> GetNumberAsync();

            Task ThrowAsynchronously();
        }

        private sealed class TestService : ITestService
        {
            public string? LastText { get; private set; }

            public int LastNumber { get; private set; }

            public object? LastOptionalValue { get; private set; }

            public void Record(
                string text,
                int number,
                object? optionalValue)
            {
                LastText = text;
                LastNumber = number;
                LastOptionalValue = optionalValue;
            }

            public int Add(int left, int right)
            {
                return left + right;
            }

            public void NoArguments()
            {
            }

            public void ThrowSynchronously()
            {
                throw new InvalidOperationException(
                    "Synchronous failure.");
            }

            public Task CompleteAsync()
            {
                return Task.CompletedTask;
            }

            public Task<int> GetNumberAsync()
            {
                return Task.FromResult(123);
            }

            public Task ThrowAsynchronously()
            {
                return Task.FromException(
                    new InvalidOperationException(
                        "Asynchronous failure."));
            }
        }
    }
}