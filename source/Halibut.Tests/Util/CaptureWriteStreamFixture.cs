using System.IO;
using System.Text;
using FluentAssertions;
using Halibut.Util;
using NUnit.Framework;

namespace Halibut.Tests.Util
{
    public class CaptureWriteStreamFixture
    {
        static readonly string LoremIpsum = 
            "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod " +
            "tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, " +
            "quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo " +
            "consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse " +
            "cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non " +
            "proident, sunt in culpa qui officia deserunt mollit anim id est laborum.";

        static readonly byte[] LoremIpsumAsBytes = Encoding.ASCII.GetBytes(LoremIpsum);

        [Test]
        public void CapturesTheFirstBytes()
        {
            using (var inner = new MemoryStream())
            using (var capture = new CaptureWriteStream(inner, 11))
            using (var outer = new StreamWriter(capture))
            {
                outer.Write(LoremIpsum);
                outer.Flush();

                capture.GetBytes().Length.Should().Be(11);
                Encoding.ASCII.GetString(capture.GetBytes()).Should().Be("Lorem ipsum");
            }
        }

        [Test]
        public void OnlyCapturesUpToTheSpecifiedCapacity()
        {
            using (var inner = new MemoryStream())
            using (var capture = new CaptureWriteStream(inner, 32))
            using (var outer = new StreamWriter(capture))
            {
                outer.Write(LoremIpsum);
                outer.Flush();
                capture.GetBytes().Length.Should().Be(32);
            }
        }

        [Test]
        public void DoesntPreventWritingMoreBytesThanTheBufferCanHold()
        {
            using (var inner = new MemoryStream())
            using (var capture = new CaptureWriteStream(inner, 32))
            using (var outer = new StreamWriter(capture))
            {
                outer.Write(LoremIpsum);
                outer.Flush();
                inner.Length.Should().Be(LoremIpsumAsBytes.Length);
            }
        }

        [Test]
        public void CanHandleStreamsThatAreShorterThanTheBuffer()
        {
            var shortLoremIpsum = LoremIpsum.Substring(0, 16);
            var shortLoremIpsumAsBytes = Encoding.ASCII.GetBytes(shortLoremIpsum);
            
            using (var inner = new MemoryStream())
            using (var capture = new CaptureWriteStream(inner, 32))
            using (var outer = new StreamWriter(capture))
            {
                outer.Write(shortLoremIpsum);
                outer.Flush();
                capture.GetBytes().Should().Equal(shortLoremIpsumAsBytes);
            }
        }
    }
}