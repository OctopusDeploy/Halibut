using System;

namespace Halibut.Tests.Transport.Streams
{
    public enum StreamMethod
    {
        Async,
        Sync,
        // This refers to the Asynchronous Programming Model (APM) Begin/End way of calling streams
        // Note that 'End' can be called either by itself after calling Begin (and block), or within the callback that is given to Begin.
        // We should test both ways of doing it
        LegacyAsyncCallEndWithinCallback,
        LegacyAsyncCallEndOutsideCallback
    }
}