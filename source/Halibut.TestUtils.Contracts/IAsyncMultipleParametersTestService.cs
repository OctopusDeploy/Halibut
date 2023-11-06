using System;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.TestUtils.Contracts
{

    public interface IAsyncMultipleParametersTestService
    {
        Task MethodReturningVoidAsync(long a, long b, CancellationToken cancellationToken);
        Task<long> AddAsync(long a, long b, CancellationToken cancellationToken);
        Task<double> AddAsync(double a, double b, CancellationToken cancellationToken);
        Task<decimal> AddAsync(decimal a, decimal b, CancellationToken cancellationToken);
        Task<string> HelloAsync(CancellationToken cancellationToken);
        Task<string> HelloAsync(string a, CancellationToken cancellationToken);
        Task<string> HelloAsync(string a, string b, CancellationToken cancellationToken);
        Task<string> HelloAsync(string a, string b, string c, CancellationToken cancellationToken);
        Task<string> HelloAsync(string a, string b, string c, string d, CancellationToken cancellationToken);
        Task<string> HelloAsync(string a, string b, string c, string d, string e, CancellationToken cancellationToken);
        Task<string> HelloAsync(string a, string b, string c, string d, string e, string f, CancellationToken cancellationToken);
        Task<string> HelloAsync(string a, string b, string c, string d, string e, string f, string g, CancellationToken cancellationToken);
        Task<string> HelloAsync(string a, string b, string c, string d, string e, string f, string g, string h, CancellationToken cancellationToken);
        Task<string> HelloAsync(string a, string b, string c, string d, string e, string f, string g, string h, string i, CancellationToken cancellationToken);
        Task<string> HelloAsync(string a, string b, string c, string d, string e, string f, string g, string h, string i, string j, CancellationToken cancellationToken);
        Task<string> HelloAsync(string a, string b, string c, string d, string e, string f, string g, string h, string i, string j, string k, CancellationToken cancellationToken);
        Task<string> AmbiguousAsync(string a, string b, CancellationToken cancellationToken);
        Task<string> AmbiguousAsync(string a, Tuple<string, string> b, CancellationToken cancellationToken);
        Task<MapLocation> GetLocationAsync(MapLocation loc, CancellationToken cancellationToken);
    }
}
