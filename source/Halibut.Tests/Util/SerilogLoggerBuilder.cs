using Serilog;
using Serilog.Formatting.Display;
using Serilog.Sinks.NUnit;

namespace Halibut.Tests.Util
{
    public class SerilogLoggerBuilder
    {
        const string OutputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level}] {Message}{NewLine}{Exception}";
        
        public ILogger Build()
        {
            return new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Sink(new NUnitSink(new MessageTemplateTextFormatter(OutputTemplate)))
                .CreateLogger();
        }
    }
}