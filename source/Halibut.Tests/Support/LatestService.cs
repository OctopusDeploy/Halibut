using System;
using System.Threading.Tasks;
using Octopus.TestPortForwarder;

namespace Halibut.Tests.Support
{
    public class LatestService : IService
    {
        public Uri ServiceUri { get; }

        public LatestService(
            HalibutRuntime service,
            Uri serviceUri,
            PortForwarder? portForwarder)
        {
            Service = service;
            ServiceUri = serviceUri;
            PortForwarder = portForwarder;
        }

        public HalibutRuntime Service { get; }
        public PortForwarder? PortForwarder { get; }

        public async ValueTask DisposeAsync()
        {
            var logger = new SerilogLoggerBuilder().Build().ForContext<LatestService>();

            logger.Information("****** ****** ****** ****** ****** ****** ******");
            logger.Information("****** SERVICE DISPOSE CALLED  ******");
            logger.Information("*     Subsequent errors should be ignored      *");
            logger.Information("****** ****** ****** ****** ****** ****** ******");

            void LogError(Exception e) => logger.Warning(e, "Ignoring error in dispose");

            await Try.DisposingAsync(Service, LogError);

            Try.CatchingError(() => PortForwarder?.Dispose(), LogError);
        }
    }
}