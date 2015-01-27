using System;

namespace Halibut.Services
{
    public interface ILogFactory
    {
        string[] GetEndpoints();
        ILog ForEndpoint(string endpoint);
    }
}