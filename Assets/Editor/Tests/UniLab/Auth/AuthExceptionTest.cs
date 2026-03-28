using NUnit.Framework;
using UniLab.Auth;

namespace UniLab.Tests.EditMode.Auth
{
    public class AuthExceptionTest
    {
        [Test]
        public void AuthException_StoresErrorCodeAndMessage()
        {
            var exception = new AuthException(AuthErrorCode.InvalidCredentials, "wrong password");

            Assert.AreEqual(AuthErrorCode.InvalidCredentials, exception.ErrorCode);
            Assert.AreEqual("wrong password", exception.Message);
        }

        [Test]
        public void AuthException_IsSystemException()
        {
            var exception = new AuthException(AuthErrorCode.Unknown, "error");

            Assert.IsInstanceOf<System.Exception>(exception);
        }

        [TestCase(AuthErrorCode.Unknown)]
        [TestCase(AuthErrorCode.InvalidCredentials)]
        [TestCase(AuthErrorCode.EmailAlreadyInUse)]
        [TestCase(AuthErrorCode.NetworkError)]
        [TestCase(AuthErrorCode.SessionExpired)]
        [TestCase(AuthErrorCode.AccountNotFound)]
        public void AuthException_AllErrorCodes_CanBeConstructed(AuthErrorCode errorCode)
        {
            var exception = new AuthException(errorCode, "test");

            Assert.AreEqual(errorCode, exception.ErrorCode);
        }
    }
}
