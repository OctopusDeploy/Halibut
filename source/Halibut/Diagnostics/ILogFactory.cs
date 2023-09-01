using System;

namespace Halibut.Diagnostics
{
    public interface ILogFactory
    {
        ILog ForEndpoint(Uri endpoint);
        ILog ForPrefix(string endPoint);
    }
}