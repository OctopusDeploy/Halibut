using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.TestUtils.Contracts;

namespace Halibut.Tests.TestServices
{
    public class AsyncMultipleParametersTestService : IAsyncMultipleParametersTestService
    {
        public async Task MethodReturningVoidAsync(long a, long b, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
        }

        public async Task<long> AddAsync(long a, long b, CancellationToken cancellationToken)
        {
            return a + b;
        }

        public async Task<double> AddAsync(double a, double b, CancellationToken cancellationToken)
        {
            return a + b;
        }

        public async Task<decimal> AddAsync(decimal a, decimal b, CancellationToken cancellationToken)
        {
            return a + b;
        }

        public async Task<string> HelloAsync(CancellationToken cancellationToken)
        {
            return "Hello";
        }

        public async Task<string> HelloAsync(string a, CancellationToken cancellationToken)
        {
            return "Hello " + a;
        }

        public async Task<string> HelloAsync(string a, string b, CancellationToken cancellationToken)
        {
            return "Hello " + string.Join(" ", a, b);
        }

        public async Task<string> HelloAsync(string a, string b, string c, CancellationToken cancellationToken)
        {
            return "Hello " + string.Join(" ", a, b, c);
        }

        public async Task<string> HelloAsync(string a, string b, string c, string d, CancellationToken cancellationToken)
        {
            return "Hello " + string.Join(" ", a, b, c, d);
        }

        public async Task<string> HelloAsync(string a, string b, string c, string d, string e, CancellationToken cancellationToken)
        {
            return "Hello " + string.Join(" ", a, b, c, d, e);
        }

        public async Task<string> HelloAsync(string a, string b, string c, string d, string e, string f, CancellationToken cancellationToken)
        {
            return "Hello " + string.Join(" ", a, b, c, d, e, f);
        }

        public async Task<string> HelloAsync(string a, string b, string c, string d, string e, string f, string g, CancellationToken cancellationToken)
        {
            return "Hello " + string.Join(" ", a, b, c, d, e, f, g);
        }

        public async Task<string> HelloAsync(string a, string b, string c, string d, string e, string f, string g, string h, CancellationToken cancellationToken)
        {
            return "Hello " + string.Join(" ", a, b, c, d, e, f, g, h);
        }

        public async Task<string> HelloAsync(string a, string b, string c, string d, string e, string f, string g, string h, string i, CancellationToken cancellationToken)
        {
            return "Hello " + string.Join(" ", a, b, c, d, e, f, g, h, i);
        }

        public async Task<string> HelloAsync(string a, string b, string c, string d, string e, string f, string g, string h, string i, string j, CancellationToken cancellationToken)
        {
            return "Hello " + string.Join(" ", a, b, c, d, e, f, g, h, i, j);
        }

        public async Task<string> HelloAsync(string a, string b, string c, string d, string e, string f, string g, string h, string i, string j, string k, CancellationToken cancellationToken)
        {
            return "Hello " + string.Join(" ", a, b, c, d, e, f, g, h, i, j, k);
        }

        public async Task<string> AmbiguousAsync(string a, string b, CancellationToken cancellationToken)
        {
            return "Hello string";
        }

        public async Task<string> AmbiguousAsync(string a, Tuple<string, string> b, CancellationToken cancellationToken)
        {
            return "Hello tuple";
        }

        public async Task<MapLocation> GetLocationAsync(MapLocation loc, CancellationToken cancellationToken)
        {
            // Swap the latitude and longitude for the round trip verification... never know where you will end up! 
            return new MapLocation { Latitude = loc.Longitude, Longitude = loc.Latitude };
        }
    }
}
