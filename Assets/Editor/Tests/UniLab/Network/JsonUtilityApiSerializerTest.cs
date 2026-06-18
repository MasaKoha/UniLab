using System;
using NUnit.Framework;
using UniLab.Network;

namespace UniLab.Tests.EditMode.Network
{
    public class JsonUtilityApiSerializerTest
    {
        private JsonUtilityApiSerializer _serializer;

        [SetUp]
        public void SetUp()
        {
            _serializer = new JsonUtilityApiSerializer();
        }

        [Test]
        public void ContentType_Is_ApplicationJson()
        {
            Assert.AreEqual("application/json", _serializer.ContentType);
        }

        [Test]
        public void Serialize_ThenDeserialize_RoundTrips()
        {
            var original = new SampleDto { Name = "UniLab", Value = 42 };

            var bytes = _serializer.Serialize(original);
            var result = _serializer.Deserialize<SampleDto>(bytes);

            Assert.AreEqual(original.Name, result.Name);
            Assert.AreEqual(original.Value, result.Value);
        }

        [Test]
        public void Serialize_ProducesUtf8WithoutBom()
        {
            var data = new SampleDto { Name = "test", Value = 1 };

            var bytes = _serializer.Serialize(data);

            Assert.IsTrue(bytes.Length >= 3, "Expected non-trivial output");
            var startsWithBom = bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            Assert.IsFalse(startsWithBom, "Output must not start with a UTF-8 BOM");
        }

        [Test]
        public void Deserialize_EmptyBytes_ReturnsDefault()
        {
            var result = _serializer.Deserialize<SampleDto>(new byte[0]);

            Assert.IsNull(result);
        }

        [Test]
        public void Deserialize_NullBytes_ReturnsDefault()
        {
            var result = _serializer.Deserialize<SampleDto>(null);

            Assert.IsNull(result);
        }

        [Serializable]
        private class SampleDto
        {
            public string Name;
            public int Value;
        }
    }
}
