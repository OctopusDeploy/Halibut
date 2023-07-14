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
            request.Payload = request.Payload.ConfigureWriterOnReceivedDataStream();
            request.Child.First = request.Child.First.ConfigureWriterOnReceivedDataStream();
            request.Child.Second = request.Child.Second.ConfigureWriterOnReceivedDataStream();
            return request;
        }

        static ComplexResponse FixResponseDataStreams(ComplexResponse response)
        {
            response.Payloads = response.Payloads.Select(x => x.ConfigureWriterOnReceivedDataStream()).ToList();
            response.Child.First = response.Child.First.ConfigureWriterOnReceivedDataStream();
            response.Child.Second = response.Child.Second.ConfigureWriterOnReceivedDataStream();
            return response;
        }
    }
}
