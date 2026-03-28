using System;
using NUnit.Framework;
using UniLab.Storage;
using UnityEngine;

namespace UniLab.Tests.EditMode.Storage
{
    public class PlayerPrefsWrapperTest
    {
        private const string Key = "unilab_test_prefs";

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(Key);
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(Key);
        }

        // --- bool ---

        [Test]
        public void Bool_SetTrue_GetReturnsTrue()
        {
            PlayerPrefsWrapper.Set(Key, true);
            Assert.IsTrue(PlayerPrefsWrapper.Get<bool>(Key));
        }

        [Test]
        public void Bool_SetFalse_GetReturnsFalse()
        {
            PlayerPrefsWrapper.Set(Key, false);
            Assert.IsFalse(PlayerPrefsWrapper.Get<bool>(Key));
        }

        [Test]
        public void Bool_MissingKey_ReturnsDefault()
        {
            Assert.IsFalse(PlayerPrefsWrapper.Get<bool>("missing_bool_key"));
        }

        // --- int ---

        [Test]
        public void Int_SetValue_GetReturnsSameValue()
        {
            PlayerPrefsWrapper.Set(Key, 12345);
            Assert.AreEqual(12345, PlayerPrefsWrapper.Get<int>(Key));
        }

        [Test]
        public void Int_MissingKey_ReturnsDefault()
        {
            Assert.AreEqual(0, PlayerPrefsWrapper.Get<int>("missing_int_key"));
        }

        // --- float ---

        [Test]
        public void Float_SetValue_GetReturnsSameValue()
        {
            PlayerPrefsWrapper.Set(Key, 3.14f);
            Assert.AreEqual(3.14f, PlayerPrefsWrapper.Get<float>(Key), delta: 0.0001f);
        }

        [Test]
        public void Float_MissingKey_ReturnsDefault()
        {
            Assert.AreEqual(0f, PlayerPrefsWrapper.Get<float>("missing_float_key"), delta: 0.0001f);
        }

        // --- string ---

        [Test]
        public void String_SetValue_GetReturnsSameValue()
        {
            PlayerPrefsWrapper.Set(Key, "hello world");
            Assert.AreEqual("hello world", PlayerPrefsWrapper.Get<string>(Key));
        }

        [Test]
        public void String_MissingKey_ReturnsEmptyString()
        {
            Assert.AreEqual(string.Empty, PlayerPrefsWrapper.Get<string>("missing_string_key"));
        }

        // --- Enum ---

        [Test]
        public void Enum_SetValue_GetReturnsSameValue()
        {
            PlayerPrefsWrapper.Set(Key, TestEnum.Beta);
            Assert.AreEqual(TestEnum.Beta, PlayerPrefsWrapper.Get<TestEnum>(Key));
        }

        [Test]
        public void Enum_MissingKey_ReturnsDefaultEnumValue()
        {
            Assert.AreEqual(TestEnum.Alpha, PlayerPrefsWrapper.Get<TestEnum>("missing_enum_key"));
        }

        // --- Unsupported type ---

        [Test]
        public void UnsupportedType_ThrowsNotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => PlayerPrefsWrapper.Set(Key, new object()));
        }

        private enum TestEnum
        {
            Alpha = 0,
            Beta = 1,
            Gamma = 2,
        }
    }
}
