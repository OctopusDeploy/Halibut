using System;
using Newtonsoft.Json;

namespace Halibut.Transport.Protocol
{
    public interface IResponseMessage
    {
        string Id { get; set; }

        ServerError Error { get; set; }

        object Result { get; set; }
    }
}