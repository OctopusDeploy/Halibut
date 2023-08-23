using Halibut.TestProxy;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Serilog.Extensions.Logging;

namespace Halibut.Tests.Support
{
    public class ProxyFactory
    {
        HttpProxyOptions options = new HttpProxyOptions();
        SerilogLoggerFactory serilogLoggerFactory = new SerilogLoggerFactory(new SerilogLoggerBuilder().Build());
        bool delaySendingSectionsOfHttpHeaders = true;

        public ProxyFactory WithDelaySendingSectionsOfHttpHeaders(bool delaySendingSectionsOfHttpHeaders)
        {
            this.delaySendingSectionsOfHttpHeaders = delaySendingSectionsOfHttpHeaders;
            return this;
        }

        public HttpProxyService Build()
        {
            return new HttpProxyService(options, delaySendingSectionsOfHttpHeaders, serilogLoggerFactory);
        }
    }
}