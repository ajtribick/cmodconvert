using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CmodConvert.Test
{
    [TestClass]
    public class VariantTest
    {
        [TestMethod]
        public void Float1Equals()
        {
            var a = new Variant(1.0f);
            var b = new Variant(1.0f);
            var c = new Variant(2.0f);

            Assert.AreEqual(a, b);
            Assert.IsTrue(a == b);
            Assert.IsFalse(a != b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
            Assert.AreNotEqual(a, c);
            Assert.IsFalse(a == c);
            Assert.IsTrue(a != c);
        }

        [TestMethod]
        public void Float2Equals()
        {
            var a = new Variant(1.0f, 1.0f);
            var b = new Variant(1.0f, 1.0f);
            var c = new Variant(1.0f, 2.0f);

            Assert.AreEqual(a, b);
            Assert.IsTrue(a == b);
            Assert.IsFalse(a != b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
            Assert.AreNotEqual(a, c);
            Assert.IsFalse(a == c);
            Assert.IsTrue(a != c);
        }

        [TestMethod]
        public void Float3Equals()
        {
            var a = new Variant(1.0f, 1.0f, 1.0f);
            var b = new Variant(1.0f, 1.0f, 1.0f);
            var c = new Variant(1.0f, 1.0f, 2.0f);

            Assert.AreEqual(a, b);
            Assert.IsTrue(a == b);
            Assert.IsFalse(a != b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
            Assert.AreNotEqual(a, c);
            Assert.IsFalse(a == c);
            Assert.IsTrue(a != c);
        }

        [TestMethod]
        public void Float4Equals()
        {
            var a = new Variant(1.0f, 1.0f, 1.0f, 1.0f);
            var b = new Variant(1.0f, 1.0f, 1.0f, 1.0f);
            var c = new Variant(1.0f, 1.0f, 1.0f, 2.0f);

            Assert.AreEqual(a, b);
            Assert.IsTrue(a == b);
            Assert.IsFalse(a != b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
            Assert.AreNotEqual(a, c);
            Assert.IsFalse(a == c);
            Assert.IsTrue(a != c);
        }

        [TestMethod]
        public void UByte4Equals()
        {
            var a = new Variant(b1: 1, b2: 1, b3: 1, b4: 1);
            var b = new Variant(b1: 1, b2: 1, b3: 1, b4: 1);
            var c = new Variant(b1: 1, b2: 1, b3: 1, b4: 2);

            Assert.AreEqual(a, b);
            Assert.IsTrue(a == b);
            Assert.IsFalse(a != b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
            Assert.AreNotEqual(a, c);
            Assert.IsFalse(a == c);
            Assert.IsTrue(a != c);
        }

        [TestMethod]
        public void DifferentTypesNotEqual()
        {
            var variants = new[]
            {
                new Variant(1.0f),
                new Variant(1.0f, 1.0f),
                new Variant(1.0f, 1.0f, 1.0f),
                new Variant(1.0f, 1.0f, 1.0f, 1.0f),
                new Variant(b1: 1, b2: 1, b3: 1, b4: 1),
            };

            for (int i = 0; i < variants.Length; ++i)
            {
                for (int j = 0; j < variants.Length; ++j)
                {
                    if (i == j) { continue; }
                    Assert.AreNotEqual(variants[i], variants[j]);
                    Assert.IsFalse(variants[i] == variants[j]);
                    Assert.IsTrue(variants[i] != variants[j]);
                }
            }
        }
    }
}