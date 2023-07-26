using System;
using System.Linq;
using Halibut.TestUtils.Contracts;
using Halibut.TestUtils.SampleProgram.Base.Utils;

namespace Halibut.TestUtils.SampleProgram.Base
{
    public class DelegateReadDataStreamService : IReadDataStreamService
    {
        readonly IReadDataStreamService readDataStreamService;

        public DelegateReadDataStreamService(IReadDataStreamService readDataStreamService)
        {
            this.readDataStreamService = readDataStreamService;
        }
        
        public long SendData(DataStream[] dataStreams)
        {
            return readDataStreamService.SendData(dataStreams.Select(x => x.ConfigureWriterOnReceivedDataStream()).ToArray());
        }
    }
}
