using System.IO;
using Halibut.Tests.Transport.Streams;
using Halibut.Transport;
using Halibut.Transport.Observability;
using Halibut.Transport.Streams;

namespace Halibut.Tests.Transport.Observability
{
    public class ByteCountingStreamIsAsyncFixture : StreamWrapperSupportsAsyncIOFixture
    {
        protected override AsyncStream WrapStream(Stream stream) => new ByteCountingStream(stream, OnDispose.DisposeInputStream);
    }
}