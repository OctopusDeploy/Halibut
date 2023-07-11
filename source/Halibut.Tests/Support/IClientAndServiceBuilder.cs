using System;
using System.Threading.Tasks;
using Octopus.TestPortForwarder;

namespace Halibut.Tests.Support
{
    // Latest to latest
    public interface IClientAndServiceBuilder : IClientAndServiceBaseBuilder
    {
        IClientAndServiceBuilder WithService<T>(Func<T> func);
    }

    public interface IClientAndServiceBaseBuilder
    {
        Task<IClientAndService> Build();
        IClientAndServiceBaseBuilder WithPortForwarding(Func<int, PortForwarder> func);
        IClientAndServiceBaseBuilder WithStandardServices();
    }
}
