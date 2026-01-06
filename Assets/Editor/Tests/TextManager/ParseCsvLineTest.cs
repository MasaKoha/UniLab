using NUnit.Framework;
using UniLab.Common.Utility;

namespace Qvou.UnityCore.TextManager.Editor.Tests
{
    public class ParseCsvLineTest
    {
        [Test]
        public void TestBasicCsvLine()
        {
            const string line = "Hello,World,123";
            var result = line.ParseCsvLine();
            Assert.AreEqual(3, result.Count);
            Assert.AreEqual("Hello", result[0]);
            Assert.AreEqual("World", result[1]);
            Assert.AreEqual("123", result[2]);
        }

        [Test]
        public void TestQuotedComma()
        {
            const string line = "Hello,\"Scene, In Unity\",123";
            var result = line.ParseCsvLine();
            Assert.AreEqual(3, result.Count);
            Assert.AreEqual("Hello", result[0]);
            Assert.AreEqual("Scene, In Unity", result[1]);
            Assert.AreEqual("123", result[2]);
        }

        [Test]
        public void TestDoubleQuoteEscape()
        {
            const string line = "Hello,\"He said \"\"Hi!\"\"\",123";
            var result = line.ParseCsvLine();
            Assert.AreEqual(3, result.Count);
            Assert.AreEqual("Hello", result[0]);
            Assert.AreEqual("He said \"Hi!\"", result[1]);
            Assert.AreEqual("123", result[2]);
        }

        [Test]
        public void TestEmptyFields()
        {
            const string line = "A,,C";
            var result = line.ParseCsvLine();
            Assert.AreEqual(3, result.Count);
            Assert.AreEqual("A", result[0]);
            Assert.AreEqual("", result[1]);
            Assert.AreEqual("C", result[2]);
        }
    }
}