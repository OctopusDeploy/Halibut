using System;

namespace Halibut.Diagnostics
{
    public interface ILogFactory
    {
        Uri[] GetEndpoints();
        string[] GetPrefixes();
        ILog ForEndpoint(Uri endpoint);
        ILog ForPrefix(string endPoint);
    }
}