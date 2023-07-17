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

        public ComplexResponse Process(ComplexRequest request)
        {
            var response = service.Process(FixRequestDataStreams(request));
            return FixResponseDataStreams(response);
        }

        static ComplexRequest FixRequestDataStreams(ComplexRequest request)
        {
            request.Payload1 = request.Payload1.ConfigureWriterOnReceivedDataStream();
            request.Payload2 = request.Payload2.ConfigureWriterOnReceivedDataStream();
            request.Child1.ChildPayload1 = request.Child1.ChildPayload1.ConfigureWriterOnReceivedDataStream();
            request.Child1.ChildPayload2 = request.Child1.ChildPayload2.ConfigureWriterOnReceivedDataStream();
            request.Child1.ListOfStreams = request.Child1.ListOfStreams.Select(x => x.ConfigureWriterOnReceivedDataStream()).ToList();
            request.Child1.ComplexPayloadSet = request.Child1.ComplexPayloadSet.Select(x => new ComplexPair<ComplexRequestEnum, DataStream>(x.Item1, x.Item2.ConfigureWriterOnReceivedDataStream())).ToHashSet();
            request.Child2.ChildPayload1 = request.Child2.ChildPayload1.ConfigureWriterOnReceivedDataStream();
            request.Child2.ChildPayload2 = request.Child2.ChildPayload2.ConfigureWriterOnReceivedDataStream();
            request.Child2.ListOfStreams = request.Child2.ListOfStreams.Select(x => x.ConfigureWriterOnReceivedDataStream()).ToList();
            request.Child2.ComplexPayloadSet = request.Child2.ComplexPayloadSet.Select(x => new ComplexPair<ComplexRequestEnum, DataStream>(x.Item1, x.Item2.ConfigureWriterOnReceivedDataStream())).ToHashSet();
            return request;
        }

        static ComplexResponse FixResponseDataStreams(ComplexResponse response)
        {
            response.Payload1 = response.Payload1.ConfigureWriterOnReceivedDataStream();
            response.Payload2 = response.Payload2.ConfigureWriterOnReceivedDataStream();
            response.Child1.ChildPayload1 = response.Child1.ChildPayload1.ConfigureWriterOnReceivedDataStream();
            response.Child1.ChildPayload2 = response.Child1.ChildPayload2.ConfigureWriterOnReceivedDataStream();
            response.Child1.ListOfStreams = response.Child1.ListOfStreams.Select(x => x.ConfigureWriterOnReceivedDataStream()).ToList();
            response.Child1.ComplexPayloadSet = response.Child1.ComplexPayloadSet.Select(x => new ComplexPair<ComplexResponseEnum, DataStream>(x.Item1, x.Item2.ConfigureWriterOnReceivedDataStream())).ToHashSet();
            response.Child2.ChildPayload1 = response.Child2.ChildPayload1.ConfigureWriterOnReceivedDataStream();
            response.Child2.ChildPayload2 = response.Child2.ChildPayload2.ConfigureWriterOnReceivedDataStream();
            response.Child2.ListOfStreams = response.Child2.ListOfStreams.Select(x => x.ConfigureWriterOnReceivedDataStream()).ToList();
            response.Child2.ComplexPayloadSet = response.Child2.ComplexPayloadSet.Select(x => new ComplexPair<ComplexResponseEnum, DataStream>(x.Item1, x.Item2.ConfigureWriterOnReceivedDataStream())).ToHashSet();
            return response;
        }
    }
}
