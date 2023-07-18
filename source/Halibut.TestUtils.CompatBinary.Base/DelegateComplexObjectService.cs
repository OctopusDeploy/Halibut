using System;
using System.Linq;
using Halibut.TestUtils.Contracts;
using Halibut.TestUtils.SampleProgram.Base.Utils;

namespace Halibut.TestUtils.SampleProgram.Base
{
    public class DelegateComplexObjectService: IComplexObjectService
    {
        readonly IComplexObjectService service;

        public DelegateComplexObjectService(IComplexObjectService service)
        {
            this.service = service;
        }
        
        public ComplexObjectMultipleDataStreams Process(ComplexObjectMultipleDataStreams request)
        {
            request.Payload1 = request.Payload1.ConfigureWriterOnReceivedDataStream();
            request.Payload2 = request.Payload2.ConfigureWriterOnReceivedDataStream();

            var response = service.Process(request);
            
            response.Payload1 = response.Payload1.ConfigureWriterOnReceivedDataStream();
            response.Payload2 = response.Payload2.ConfigureWriterOnReceivedDataStream();

            return response;
        }

        public ComplexObjectMultipleChildren Process(ComplexObjectMultipleChildren request)
        {
            request.Child1.ChildPayload1 = request.Child1.ChildPayload1.ConfigureWriterOnReceivedDataStream();
            request.Child1.ChildPayload2 = request.Child1.ChildPayload2.ConfigureWriterOnReceivedDataStream();
            request.Child1.ListOfStreams = request.Child1.ListOfStreams.Select(x => x.ConfigureWriterOnReceivedDataStream()).ToList();
            request.Child2.ComplexPayloadSet = request.Child2.ComplexPayloadSet.Select(x => new ComplexPair<DataStream>(x.EnumValue, x.Payload.ConfigureWriterOnReceivedDataStream())).ToHashSet();
            request.Child1.ListOfStreams = request.Child1.ListOfStreams.Select(x => x.ConfigureWriterOnReceivedDataStream()).ToList();
            request.Child2.ComplexPayloadSet = request.Child2.ComplexPayloadSet.Select(x => new ComplexPair<DataStream>(x.EnumValue, x.Payload.ConfigureWriterOnReceivedDataStream())).ToHashSet();

            var response = service.Process(request);
            
            response.Child1.ChildPayload1 = response.Child1.ChildPayload1.ConfigureWriterOnReceivedDataStream();
            response.Child1.ChildPayload2 = response.Child1.ChildPayload2.ConfigureWriterOnReceivedDataStream();
            response.Child1.ListOfStreams = response.Child1.ListOfStreams.Select(x => x.ConfigureWriterOnReceivedDataStream()).ToList();
            response.Child2.ComplexPayloadSet = response.Child2.ComplexPayloadSet.Select(x => new ComplexPair<DataStream>(x.EnumValue, x.Payload.ConfigureWriterOnReceivedDataStream())).ToHashSet();
            response.Child2.ComplexPayloadSet = response.Child2.ComplexPayloadSet.Select(x => new ComplexPair<DataStream>(x.EnumValue, x.Payload.ConfigureWriterOnReceivedDataStream())).ToHashSet();

            return response;
        }
    }
}
