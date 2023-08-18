using System;

namespace Halibut.Tests.Transport.Streams
{
    public enum StreamReadMethod
    {
        Read,
        ReadByte,
        ReadAsync,
#if !NETFRAMEWORK
        ReadAsyncForMemoryByteArray,
#endif
        BeginReadEndWithinCallback,
        BeginReadEndOutsideCallback
    }
}