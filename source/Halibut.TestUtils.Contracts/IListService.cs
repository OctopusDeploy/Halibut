using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.TestUtils.Contracts
{
    public interface IListService
    {
        List<string> GetList();
    }


    public interface IAsyncListService
    {
        Task<List<string>> GetListAsync(CancellationToken cancellationToken);
    }
}