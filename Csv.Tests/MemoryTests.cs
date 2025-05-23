﻿using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Csv.Tests
{
    [TestClass]
    public class MemoryTests
    {
        [TestMethod]
        public void StartsWith()
        {
            Assert.IsFalse("".AsMemory().StartsWith("test"));
            Assert.IsFalse("te".AsMemory().StartsWith("test"));
            Assert.IsFalse("something".AsMemory().StartsWith("test"));
            Assert.IsTrue("test".AsMemory().StartsWith("test"));
            Assert.IsTrue("testing".AsMemory().StartsWith("test"));
        }

        [TestMethod]
        public void EndsWith()
        {
            Assert.IsFalse("".AsMemory().EndsWith("test"));
            Assert.IsFalse("st".AsMemory().EndsWith("test"));
            Assert.IsFalse("something".AsMemory().EndsWith("test"));
            Assert.IsTrue("test".AsMemory().EndsWith("test"));
            Assert.IsTrue("pretest".AsMemory().EndsWith("test"));
        }

        [TestMethod]
        public void Unescape()
        {
            var test = "test#-test".AsMemory();
            var unescaped = test.Unescape('#', '-');
            Assert.AreEqual("test-test", unescaped.AsString());
        }

        [TestMethod]
        public void UnescapeQuoted()
        {
            var test = "test\"\"test".AsMemory();
            var unescaped = test.Unescape('"', '"');
            Assert.AreEqual("test\"test", unescaped.AsString());
        }

        [TestMethod]
        public void UnescapeNothingToEscape()
        {
            var test = "test.test".AsMemory();
            var unescaped = test.Unescape('#', '-');
            Assert.AreEqual("test.test", unescaped.AsString());
            Assert.AreEqual(test, unescaped);
        }

        [TestMethod]
        public void UnescapeOnlyPartial()
        {
            var test = "test#test".AsMemory();
            var unescaped = test.Unescape('#', '-');
            Assert.AreEqual("test#test", unescaped.AsString());
            Assert.AreEqual(test, unescaped);
        }

        [TestMethod]
        public void UnescapeSameEscapeCharacter()
        {
            var test = "test--test".AsMemory();
            var unescaped = test.Unescape('-', '-');
            Assert.AreEqual("test-test", unescaped.AsString());
        }

        [TestMethod]
        public void UnescapeMultipleEscaped()
        {
            var test = "a#-b#-c".AsMemory();
            var unescaped = test.Unescape('#', '-');
            Assert.AreEqual("a-b-c", unescaped.AsString());
        }

        [TestMethod]
        public void ReadLine()
        {
            var csv = "a,b,c\nd,e,f\ng,h,i".AsMemory();

            var pos = 0;
            var firstLine = csv.ReadLine(ref pos);
            Assert.AreEqual(6, pos);
            Assert.AreEqual("a,b,c", firstLine.AsString());
            var secondLine = csv.ReadLine(ref pos);
            Assert.AreEqual(12, pos);
            Assert.AreEqual("d,e,f", secondLine.AsString());
            var thirdLine = csv.ReadLine(ref pos);
            Assert.AreEqual(17, pos);
            Assert.AreEqual("g,h,i", thirdLine.AsString());
            var empty = csv.ReadLine(ref pos);
            Assert.IsTrue(empty.IsEmpty);
        }

        [TestMethod]
        public void ReadLineTrailing()
        {
            var csv = "a,b,c\nd,e,f\ng,h,i\n".AsMemory();

            var pos = 0;
            var firstLine = csv.ReadLine(ref pos);
            Assert.AreEqual(6, pos);
            Assert.AreEqual("a,b,c", firstLine.AsString());
            var secondLine = csv.ReadLine(ref pos);
            Assert.AreEqual(12, pos);
            Assert.AreEqual("d,e,f", secondLine.AsString());
            var thirdLine = csv.ReadLine(ref pos);
            Assert.AreEqual(18, pos);
            Assert.AreEqual("g,h,i", thirdLine.AsString());
            var empty = csv.ReadLine(ref pos);
            Assert.IsTrue(empty.IsEmpty);
        }
    }
}