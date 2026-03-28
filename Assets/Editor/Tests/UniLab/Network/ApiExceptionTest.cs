using NUnit.Framework;
using UniLab.Network;

namespace UniLab.Tests.EditMode.Network
{
    public class ApiExceptionTest
    {
        [Test]
        public void ApiException_StoresStatusCodeAndBody()
        {
            var exception = new ApiException(500, "Internal Server Error", "server error");

            Assert.AreEqual(500, exception.StatusCode);
            Assert.AreEqual("Internal Server Error", exception.ResponseBody);
            Assert.AreEqual("server error", exception.Message);
        }

        [Test]
        public void UnauthorizedException_HasStatus401()
        {
            var exception = new UnauthorizedException("body");

            Assert.AreEqual(401, exception.StatusCode);
            Assert.AreEqual("body", exception.ResponseBody);
        }

        [Test]
        public void UnauthorizedException_IsApiException()
        {
            var exception = new UnauthorizedException("body");

            Assert.IsInstanceOf<ApiException>(exception);
        }

        [Test]
        public void TooManyRequestsException_HasStatus429()
        {
            var exception = new TooManyRequestsException("rate limited");

            Assert.AreEqual(429, exception.StatusCode);
        }

        [Test]
        public void ServiceUnavailableException_HasStatus503()
        {
            var exception = new ServiceUnavailableException("down");

            Assert.AreEqual(503, exception.StatusCode);
        }

        [Test]
        public void AllDerivedExceptions_AreApiException()
        {
            Assert.IsInstanceOf<ApiException>(new UnauthorizedException(""));
            Assert.IsInstanceOf<ApiException>(new TooManyRequestsException(""));
            Assert.IsInstanceOf<ApiException>(new ServiceUnavailableException(""));
        }
    }
}
