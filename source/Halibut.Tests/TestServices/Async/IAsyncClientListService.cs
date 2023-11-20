using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Halibut.Tests.TestServices.Async
{
    public interface IAsyncClientListService
    {
        Task<List<string>> GetListAsync();
    }
}