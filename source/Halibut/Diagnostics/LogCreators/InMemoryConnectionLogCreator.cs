using System;

namespace Halibut.Diagnostics.LogCreators
{
    /// <summary>
    /// For performance create only one of these and cache it.
    ///
    /// The issue is creating these are expensive, so if one of these is
    /// created each time a new ILog is requested or worse each time a log
    /// message is to be written, Halibut can slow down significantly. Thus
    /// try to make exactly one of these for the lifetime of a halibut runtime.
    /// 
    /// Creates a new ILog object on each call
    /// </summary>
    public class InMemoryConnectionLogCreator : ICreateNewILog
    {
        readonly Logging.ILog logger = LogProvider.GetLogger("Halibut");

        public ILog CreateNewForPrefix(string prefix)
        {
            return new InMemoryConnectionLog(prefix, logger);
        }
    }
}