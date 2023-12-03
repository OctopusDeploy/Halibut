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
            return FixDataStreams(service.Process(FixDataStreams(request)));
        }

        ComplexObjectMultipleDataStreams FixDataStreams(ComplexObjectMultipleDataStreams complexObject)
        {
            complexObject.Payload1 = complexObject.Payload1!.ConfigureWriterOnReceivedDataStream();
            complexObject.Payload2 = complexObject.Payload2!.ConfigureWriterOnReceivedDataStream();
            return complexObject;
        }

        public ComplexObjectMultipleChildren Process(ComplexObjectMultipleChildren request)
        {
            return FixDataStreams(service.Process(FixDataStreams(request)));
        }

        public ComplexObjectWithInheritance Process(ComplexObjectWithInheritance request)
        {
            return service.Process(request);
        }

        ComplexObjectMultipleChildren FixDataStreams(ComplexObjectMultipleChildren complexObject)
        {
            complexObject.Child1!.ChildPayload1 = complexObject.Child1.ChildPayload1!.ConfigureWriterOnReceivedDataStream();
            complexObject.Child1.ChildPayload2 = complexObject.Child1.ChildPayload2!.ConfigureWriterOnReceivedDataStream();
            complexObject.Child1.ListOfStreams = complexObject.Child1.ListOfStreams!.Select(x => x.ConfigureWriterOnReceivedDataStream()).ToList();
            complexObject.Child2!.ComplexPayloadSet = complexObject.Child2.ComplexPayloadSet!.Select(x => new ComplexPair<DataStream>(x.EnumValue, x.Payload.ConfigureWriterOnReceivedDataStream())).ToHashSet();
            complexObject.Child2.ComplexPayloadSet = complexObject.Child2.ComplexPayloadSet.Select(x => new ComplexPair<DataStream>(x.EnumValue, x.Payload.ConfigureWriterOnReceivedDataStream())).ToHashSet();
            return complexObject;
        }
    }
}
