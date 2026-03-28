using NUnit.Framework;
using UniLab.Auth;

namespace UniLab.Tests.EditMode.Auth
{
    public class AuthUserTest
    {
        [Test]
        public void AuthUser_DefaultConstructor_AnonymousIsFalse()
        {
            var user = new AuthUser();

            Assert.IsFalse(user.IsAnonymous);
        }

        [Test]
        public void AuthUser_IsAnonymous_True_WhenSet()
        {
            var user = new AuthUser { IsAnonymous = true };

            Assert.IsTrue(user.IsAnonymous);
        }

        [Test]
        public void AuthUser_FieldsRoundTrip()
        {
            var user = new AuthUser
            {
                UserId = "uid-123",
                Email = "test@example.com",
                IsAnonymous = false,
                AccessToken = "access_token",
                RefreshToken = "refresh_token",
                ExpiresAt = 9999999999L,
            };

            Assert.AreEqual("uid-123", user.UserId);
            Assert.AreEqual("test@example.com", user.Email);
            Assert.IsFalse(user.IsAnonymous);
            Assert.AreEqual("access_token", user.AccessToken);
            Assert.AreEqual("refresh_token", user.RefreshToken);
            Assert.AreEqual(9999999999L, user.ExpiresAt);
        }
    }
}
