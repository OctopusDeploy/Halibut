using System;

namespace Halibut.TestUtils.Contracts
{
    public interface IReadDataStreamService
    {
        long SendData(params DataStream[] dataStreams);
    }
}
