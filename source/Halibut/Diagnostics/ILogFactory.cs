using System;

namespace Halibut.Diagnostics
{
    public interface ILogFactory
    {
        Uri[] GetEndpoints();
        ILog ForEndpoint(Uri endpoint);
    }
}