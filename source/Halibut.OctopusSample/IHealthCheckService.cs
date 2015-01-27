using System;

namespace Halibut.OctopusSample
{
    public interface IHealthCheckService
    {
        bool IsOnline();
    }
}