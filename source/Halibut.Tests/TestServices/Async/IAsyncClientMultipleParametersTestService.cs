using System;
using System.Threading.Tasks;
using Halibut.TestUtils.Contracts;

namespace Halibut.Tests.TestServices.Async
{
    public interface IAsyncClientMultipleParametersTestService
    {
        Task MethodReturningVoidAsync(long a, long b);
        Task<long> AddAsync(long a, long b);
        Task<double> AddAsync(double a, double b);
        Task<decimal> AddAsync(decimal a, decimal b);
        Task<string> HelloAsync();
        Task<string> HelloAsync(string a);
        Task<string> HelloAsync(string a, string b);
        Task<string> HelloAsync(string a, string b, string c);
        Task<string> HelloAsync(string a, string b, string c, string d);
        Task<string> HelloAsync(string a, string b, string c, string d, string e);
        Task<string> HelloAsync(string a, string b, string c, string d, string e, string f);
        Task<string> HelloAsync(string a, string b, string c, string d, string e, string f, string g);
        Task<string> HelloAsync(string a, string b, string c, string d, string e, string f, string g, string h);
        Task<string> HelloAsync(string a, string b, string c, string d, string e, string f, string g, string h, string i);
        Task<string> HelloAsync(string a, string b, string c, string d, string e, string f, string g, string h, string i, string j);
        Task<string> HelloAsync(string a, string b, string c, string d, string e, string f, string g, string h, string i, string j, string k);
        Task<string> AmbiguousAsync(string a, string b);
        Task<string> AmbiguousAsync(string a, Tuple<string, string> b);
        Task<MapLocation> GetLocationAsync(MapLocation loc);
        
    }
}