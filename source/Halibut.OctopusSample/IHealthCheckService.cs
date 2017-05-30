using System;
using System.Threading.Tasks;

namespace Halibut.OctopusSample
{
    public interface IHealthCheckService
    {
        Task<bool> IsOnline();
    }
}