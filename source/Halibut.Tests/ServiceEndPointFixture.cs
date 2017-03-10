using FluentAssertions;
using Newtonsoft.Json;
using Xunit;

namespace Halibut.Tests
{
    public class ServiceEndPointFixture
    {
        [Fact]
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