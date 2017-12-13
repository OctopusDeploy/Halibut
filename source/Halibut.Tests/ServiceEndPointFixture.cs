using FluentAssertions;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class ServiceEndPointFixture
    {
        [Test]
        public void IncompleteEndPointCanBeDeserialized()
        {
            var json = "{BaseUri: \"http://google.com\", RemoteThumbprint: \"AAAA\"}";
            var result = JsonConvert.DeserializeObject<ServiceEndPoint>(json);

            result.BaseUri.Host.Should().Be("google.com");
            result.RemoteThumbprint.Should().Be("AAAA");
            Assert.Null(result.Proxy);
        }
    }
}