using System.Text;
using NUnit.Framework;
using UniLab.Network;

namespace UniLab.Tests.EditMode.Network
{
    public class ApiExceptionTest
    {
        [Test]
        public void ApiException_StoresStatusCodeAndBody()
        {
            var responseBody = Encoding.UTF8.GetBytes("Internal Server Error");
            var exception = new ApiException(500, responseBody, "server error");

            Assert.AreEqual(500, exception.StatusCode);
            Assert.AreEqual("Internal Server Error", exception.ResponseBodyAsString);
            Assert.AreEqual("server error", exception.Message);
        }

        [Test]
        public void UnauthorizedException_HasStatus401()
        {
            var responseBody = Encoding.UTF8.GetBytes("body");
            var exception = new UnauthorizedException(responseBody);

            Assert.AreEqual(401, exception.StatusCode);
            Assert.AreEqual("body", exception.ResponseBodyAsString);
        }

        [Test]
        public void UnauthorizedException_IsApiException()
        {
            var exception = new UnauthorizedException(Encoding.UTF8.GetBytes("body"));

            Assert.IsInstanceOf<ApiException>(exception);
        }

        [Test]
        public void TooManyRequestsException_HasStatus429()
        {
            var exception = new TooManyRequestsException(Encoding.UTF8.GetBytes("rate limited"));

            Assert.AreEqual(429, exception.StatusCode);
        }

        [Test]
        public void ServiceUnavailableException_HasStatus503()
        {
            var exception = new ServiceUnavailableException(Encoding.UTF8.GetBytes("down"));

            Assert.AreEqual(503, exception.StatusCode);
        }

        [Test]
        public void AllDerivedExceptions_AreApiException()
        {
            Assert.IsInstanceOf<ApiException>(new UnauthorizedException(new byte[0]));
            Assert.IsInstanceOf<ApiException>(new TooManyRequestsException(new byte[0]));
            Assert.IsInstanceOf<ApiException>(new ServiceUnavailableException(new byte[0]));
        }

        [Test]
        public void ResponseBodyAsString_NullSafe_WhenResponseBodyIsNull()
        {
            var exception = new ApiException(500, null, "server error");

            Assert.AreEqual(string.Empty, exception.ResponseBodyAsString);
        }
    }
}
