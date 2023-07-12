using System;
using System.Threading.Tasks;
using Octopus.TestPortForwarder;

namespace Halibut.Tests.Support
{
    // All ClientAndService builders will implement this
    public interface IClientAndServiceBaseBuilder
    {
        Task<IClientAndService> Build();
        IClientAndServiceBaseBuilder WithPortForwarding(Func<int, PortForwarder> func);
        IClientAndServiceBaseBuilder WithStandardServices();
    }
}
