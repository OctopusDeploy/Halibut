using System;

namespace Halibut.Diagnostics.LogCreators
{
    /// <summary>
    /// Creates loggers that log to the in memory queue only and to no other place.
    /// </summary>
    public class InMemoryOnlyConnectionLogCreator : ICreateNewILog
    {
        public ILog CreateNewForPrefix(string prefix)
        {
            return new InMemoryConnectionLog(prefix, null);
        }
    }
}