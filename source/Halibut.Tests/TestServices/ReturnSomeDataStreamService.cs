using System;

namespace Halibut.Tests.TestServices
{
    public class ReturnSomeDataStreamService : IReturnSomeDataStreamService
    {
        readonly Func<DataStream> createDataStream;

        public ReturnSomeDataStreamService(Func<DataStream> createDataStream)
        {
            this.createDataStream = createDataStream;
        }

        public DataStream SomeDataStream()
        {
            return createDataStream();
        }
    }
}