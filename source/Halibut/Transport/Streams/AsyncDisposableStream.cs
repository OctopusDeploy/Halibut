using System;
using System.IO;
using System.Threading.Tasks;

namespace Halibut.Transport.Streams
{
    // Streams do not implement IAsyncDisposable in .NET 4.8. So to prevent putting #if everywhere, deal with all this here.
    public abstract class AsyncDisposableStream : Stream
#if NETFRAMEWORK
        , IAsyncDisposable
#endif
    {
#if NETFRAMEWORK
        public abstract ValueTask DisposeAsync();
#endif
    }
}