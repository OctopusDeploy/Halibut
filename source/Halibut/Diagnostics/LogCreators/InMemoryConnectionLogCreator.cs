using System;
using Halibut.Logging;

namespace Halibut.Diagnostics.LogCreators
{
    /// <summary>
    /// For performance create only one of these and cache it.
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