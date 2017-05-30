using System;
using Halibut.Logging;
using Xunit.Abstractions;

namespace Halibut.Tests
{
    public class XunitLogProvider : ILogProvider
    {
        readonly ITestOutputHelper output;

        public XunitLogProvider(ITestOutputHelper output)
        {
            this.output = output;
        }

        public Logger GetLogger(string name)
        {
            return GetLogger;
        }

        bool GetLogger(LogLevel logLevel, Func<string> messageFunc, Exception exception, params object[] formatParameters)
        {
            if (logLevel < LogLevel.Info)
            {
                return true;
            }

            var message = $"[{logLevel}] {String.Format(messageFunc(), formatParameters)}";
            if (exception != null)
            {
                message += $"{Environment.NewLine}{exception}";
            }

            output.WriteLine(message);
           
            return true;
        }

        public IDisposable OpenNestedContext(string message)
        {
            return new NoOpDisposable();
        }

        class NoOpDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }

        public IDisposable OpenMappedContext(string key, string value)
        {
            return new NoOpDisposable();
        }
    }
}