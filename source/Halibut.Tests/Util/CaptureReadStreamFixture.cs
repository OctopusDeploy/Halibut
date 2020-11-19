using System.IO;
using System.Text;
using FluentAssertions;
using Halibut.Util;
using NUnit.Framework;

namespace Halibut.Tests.Util
{
    public class CaptureReadStreamFixture
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
            using (var inner = new MemoryStream(LoremIpsumAsBytes))
            using (var capture = new CaptureReadStream(inner, 11))
            using (var outer = new StreamReader(capture))
            {
                outer.ReadToEnd();

                capture.GetBytes().Length.Should().Be(11);
                Encoding.ASCII.GetString(capture.GetBytes()).Should().Be("Lorem ipsum");
            }
        }

        [Test]
        public void OnlyCapturesUpToTheSpecifiedCapacity()
        {
            using (var inner = new MemoryStream(LoremIpsumAsBytes))
            using (var capture = new CaptureReadStream(inner, 32))
            using (var outer = new StreamReader(capture))
            {
                outer.ReadToEnd();
                capture.GetBytes().Length.Should().Be(32);
            }
        }

        [Test]
        public void DoesntPreventReadingMoreBytesThanTheBufferCanHold()
        {
            using (var inner = new MemoryStream(LoremIpsumAsBytes))
            using (var capture = new CaptureReadStream(inner, 32))
            using (var outer = new StreamReader(capture))
            {
                outer.ReadToEnd().Should().Be(LoremIpsum);
            }
        }

        [Test]
        public void CanHandleInputStreamThatAreShorterThanTheBuffer()
        {
            var shortLoremIpsum = LoremIpsum.Substring(0, 16);
            var shortLoremIpsumAsBytes = Encoding.ASCII.GetBytes(shortLoremIpsum);
            
            using (var inner = new MemoryStream(shortLoremIpsumAsBytes))
            using (var capture = new CaptureReadStream(inner, 32))
            using (var outer = new StreamReader(capture))
            {
                outer.ReadToEnd().Should().Be(shortLoremIpsum);
                capture.GetBytes().Should().Equal(shortLoremIpsumAsBytes);
            }
        }
    }
}