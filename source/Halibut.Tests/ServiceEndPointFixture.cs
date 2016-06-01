using Newtonsoft.Json;
using NUnit.Framework;

namespace Halibut.Tests
{
    [TestFixture]
    public class ServiceEndPointFixture
    {
        [Test]
        public void IncompleteEndPointCanBeDeserialized()
        {
            var json = "{BaseUri: \"http://google.com\", RemoteThumbprint: \"AAAA\"}";
            var result = JsonConvert.DeserializeObject<ServiceEndPoint>(json);

            Assert.AreEqual("google.com", result.BaseUri.Host);
            Assert.AreEqual("AAAA", result.RemoteThumbprint);
            Assert.Null(result.Proxy);
        }
    }
}