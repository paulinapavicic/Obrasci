using FluentAssertions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Obrasci.Controllers;
using Xunit;

namespace Tests.Unit
{
    public class CsrfDemoControllerTests
    {
        private readonly Mock<IAntiforgery> _antiforgery = new();
        private readonly CsrfDemoController _controller;

        public CsrfDemoControllerTests()
        {
            _controller = new CsrfDemoController(_antiforgery.Object);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
        }

        [Fact]
        public void GetToken_returns_request_token_from_antiforgery_service()
        {
            const string expectedToken = "csrf-request-token";

            var tokenSet = new AntiforgeryTokenSet(
                requestToken: expectedToken,
                cookieToken: "csrf-cookie-token",
                formFieldName: "__RequestVerificationToken",
                headerName: "RequestVerificationToken");

            _antiforgery
                .Setup(service => service.GetAndStoreTokens(
                    It.IsAny<HttpContext>()))
                .Returns(tokenSet);

          
            var result = _controller.GetToken();

            var ok = result.Should().BeOfType<OkObjectResult>().Subject;

            ok.StatusCode.Should().Be(200);
            ok.Value.Should().NotBeNull();

            var csrfTokenProperty = ok.Value!
                .GetType()
                .GetProperty("csrfToken");

            csrfTokenProperty.Should().NotBeNull();

            csrfTokenProperty!.GetValue(ok.Value)
                .Should()
                .Be(expectedToken);

            _antiforgery.Verify(
                service => service.GetAndStoreTokens(
                    _controller.HttpContext),
                Times.Once);
        }

        [Fact]
        public async Task ChangeSetting_when_antiforgery_validation_passes_returns_ok()
        {
           
            _antiforgery
                .Setup(service => service.ValidateRequestAsync(
                    It.IsAny<HttpContext>()))
                .Returns(Task.CompletedTask);

           
            var result = await _controller.ChangeSetting();

           
            var ok = result.Should().BeOfType<OkObjectResult>().Subject;

            ok.StatusCode.Should().Be(200);

            var messageProperty = ok.Value!
                .GetType()
                .GetProperty("message");

            messageProperty.Should().NotBeNull();

            messageProperty!.GetValue(ok.Value)
                .Should()
                .Be(
                    "State-changing request accepted because CSRF validation passed.");

            _antiforgery.Verify(
                service => service.ValidateRequestAsync(
                    _controller.HttpContext),
                Times.Once);
        }

        [Fact]
        public async Task ChangeSetting_when_antiforgery_validation_fails_propagates_exception()
        {
          
            _antiforgery
                .Setup(service => service.ValidateRequestAsync(
                    It.IsAny<HttpContext>()))
                .ThrowsAsync(new AntiforgeryValidationException(
                    "The antiforgery token is invalid."));

           
            Func<Task> act = () => _controller.ChangeSetting();

            await act.Should()
                .ThrowAsync<AntiforgeryValidationException>()
                .WithMessage("The antiforgery token is invalid.");
        }
    }
}