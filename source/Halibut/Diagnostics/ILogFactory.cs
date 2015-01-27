using System;

namespace Halibut.Diagnostics
{
    public interface ILogFactory
    {
        string[] GetEndpoints();
        ILog ForEndpoint(string endpoint);
    }
}