using System;
using Newtonsoft.Json;

namespace Halibut.Transport.Protocol
{
    public interface IRequestMessage
    {
        string Id { get; set; }

        Guid ActivityId { get; set; }

        ServiceEndPoint Destination { get; set; }

        string ServiceName { get; set; }

        string MethodName { get; set; }

        object[] Params { get; set; }
    }
}