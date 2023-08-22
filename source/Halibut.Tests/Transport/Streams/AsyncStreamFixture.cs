using System;
using System.IO;
using Halibut.Tests.Support.Streams;
using Halibut.Transport.Streams;

namespace Halibut.Tests.Transport.Streams
{
    public class AsyncStreamFixture : StreamWrapperSupportsAsyncIOFixture
    {
        protected override AsyncStream WrapStream(Stream stream) => new NoOpAsyncStream(stream);
    }
}