using System;
using Halibut.Diagnostics.LogWriters;

namespace Halibut.Diagnostics.LogCreators
{
    public class AggregateLogWriterLogCreator : ICreateNewILog
    {
        readonly ICreateNewILog logCreator;
        readonly Func<string, ILogWriter[]> logWriterFactoryForPrefix;

        public AggregateLogWriterLogCreator(ICreateNewILog logCreator, Func<string, ILogWriter[]> logWriterFactoryForPrefix)
        {
            this.logCreator = logCreator;
            this.logWriterFactoryForPrefix = logWriterFactoryForPrefix;
        }

        public ILog CreateNewForPrefix(string prefix)
        {
            return new AggregateLogWriter(logCreator.CreateNewForPrefix(prefix), logWriterFactoryForPrefix(prefix));
        }
    }
} 