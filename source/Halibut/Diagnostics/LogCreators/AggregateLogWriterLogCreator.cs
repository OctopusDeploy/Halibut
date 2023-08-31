using System;
using Halibut.Diagnostics.LogWriters;

namespace Halibut.Diagnostics.LogCreators
{
    public class AggregateLogWriterLogCreator : ICreateNewILog
    {
        readonly ICreateNewILog nonCachingLogFactory;
        readonly Func<string, ILogWriter[]> logWriterFactoryForPrefix;

        public AggregateLogWriterLogCreator(ICreateNewILog nonCachingLogFactory, Func<string, ILogWriter[]> logWriterFactoryForPrefix)
        {
            this.nonCachingLogFactory = nonCachingLogFactory;
            this.logWriterFactoryForPrefix = logWriterFactoryForPrefix;
        }

        public ILog CreateNewForPrefix(string prefix)
        {
            return new AggregateLogWriterLog(nonCachingLogFactory.CreateNewForPrefix(prefix), logWriterFactoryForPrefix(prefix));
        }
    }
}