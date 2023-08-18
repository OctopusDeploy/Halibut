using System;

namespace Halibut.Tests.Transport.Streams
{
    public enum StreamCopyToMethod
    {
        CopyTo,
        CopyToWithBufferSize,
        CopyToAsync,
        CopyToAsyncWithBufferSize,
    }
}