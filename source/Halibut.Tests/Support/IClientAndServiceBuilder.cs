using System;
using System.Threading.Tasks;
using Octopus.TestPortForwarder;

namespace Halibut.Tests.Support
{
    public interface IClientAndServiceBuilder
    {
        Task<IClientAndService> Build();
        IClientAndServiceBuilder WithService<T>(Func<T> func);
        IClientAndServiceBuilder WithPortForwarding(Func<int, PortForwarder> func);
    }
}