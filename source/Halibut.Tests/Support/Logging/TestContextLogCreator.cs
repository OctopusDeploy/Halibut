using System;
using Halibut.Diagnostics;
using Halibut.Diagnostics.LogCreators;
using ILog = Halibut.Diagnostics.ILog;

namespace Halibut.Tests.Support.Logging
{
    public class TestContextLogCreator : ICreateNewILog
    {
        readonly string name;
        readonly LogLevel logLevel;

        public TestContextLogCreator(string name, LogLevel logLevel)
        {
            this.name = name;
            this.logLevel = logLevel;
        }

        public ILog CreateNewForPrefix(string prefix)
        {
            return new TestContextConnectionLog(prefix, name, logLevel);
        }
    }
}