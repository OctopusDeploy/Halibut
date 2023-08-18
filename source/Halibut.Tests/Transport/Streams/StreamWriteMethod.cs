using System;

namespace Halibut.Tests.Transport.Streams
{
    public enum StreamWriteMethod
    {
        Write,
        WriteByte,
        WriteAsync,
#if !NETFRAMEWORK
        WriteAsyncForMemoryByteArray,
#endif
        BeginWriteEndWithinCallback,
        BeginWriteEndOutsideCallback
    }
}