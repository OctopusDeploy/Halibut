using System;
using Halibut.Diagnostics.LogWriters;

namespace Halibut.Diagnostics.LogCreators
{
    public class AggregateLogWriterLogCreator : ICreateNewILog
    {
        readonly ICreateNewILog logCreator;
        readonly Func<string, ILog[]> logWriterFactoryForPrefix;

        public AggregateLogWriterLogCreator(ICreateNewILog logCreator, Func<string, ILog[]> logWriterFactoryForPrefix)
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