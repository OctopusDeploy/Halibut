using System;

namespace Halibut.Diagnostics.LogCreators
{
    public interface ICreateNewILog
    {
        ILog CreateNewForPrefix(string prefix);
    }
}