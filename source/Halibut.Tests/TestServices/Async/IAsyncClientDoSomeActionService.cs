﻿using System.Threading.Tasks;

namespace Halibut.Tests.TestServices.Async
{
    public interface IAsyncClientDoSomeActionService
    {
        Task ActionAsync();
    }
}
