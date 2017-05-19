using System;
using System.Threading.Tasks;

namespace Halibut.Tests.TestServices
{
    public class SupportedServices : ISupportedServices
    {
        public Task MethodReturningVoid(long a, long b)
        {
            return Task.FromResult(0);
        }

        public Task<long> Add(long a, long b)
        {
           return Task.FromResult(a + b);
        }

        public Task<double> Add(double a, double b)
        {
            return Task.FromResult(a + b);
        }

        public Task<decimal> Add(decimal a, decimal b)
        {
            return Task.FromResult(a + b);
        }

        public Task<string> Hello()
        {
            return Task.FromResult("Hello");
        }

        public Task<string> Hello(string a)
        {
            return Task.FromResult("Hello " + a);
        }

        public Task<string> Hello(string a, string b)
        {
            return Task.FromResult("Hello " + string.Join(" ", a, b));
        }

        public Task<string> Hello(string a, string b, string c)
        {
            return Task.FromResult("Hello " + string.Join(" ", a, b, c));
        }

        public Task<string> Hello(string a, string b, string c, string d)
        {
            return Task.FromResult("Hello " + string.Join(" ", a, b, c, d));
        }

        public Task<string> Hello(string a, string b, string c, string d, string e)
        {
            return Task.FromResult("Hello " + string.Join(" ", a, b, c, d, e));
        }

        public Task<string> Hello(string a, string b, string c, string d, string e, string f)
        {
            return Task.FromResult("Hello " + string.Join(" ", a, b, c, d, e, f));
        }

        public Task<string> Hello(string a, string b, string c, string d, string e, string f, string g)
        {
            return Task.FromResult("Hello " + string.Join(" ", a, b, c, d, e, f, g));
        }

        public Task<string> Hello(string a, string b, string c, string d, string e, string f, string g, string h)
        {
            return Task.FromResult("Hello " + string.Join(" ", a, b, c, d, e, f, g, h));
        }

        public Task<string> Hello(string a, string b, string c, string d, string e, string f, string g, string h, string i)
        {
            return Task.FromResult("Hello " + string.Join(" ", a, b, c, d, e, f, g, h, i));
        }

        public Task<string> Hello(string a, string b, string c, string d, string e, string f, string g, string h, string i, string j)
        {
            return Task.FromResult("Hello " + string.Join(" ", a, b, c, d, e, f, g, h, i, j));
        }

        public Task<string> Hello(string a, string b, string c, string d, string e, string f, string g, string h, string i, string j, string k)
        {
            return Task.FromResult("Hello " + string.Join(" ", a, b, c, d, e, f, g, h, i, j, k));
        }

        public Task<string> Ambiguous(string a, string b)
        {
            return Task.FromResult("Hello string");
        }

        public Task<string> Ambiguous(string a, Tuple<string, string> b)
        {
            return Task.FromResult("Hello tuple");
        }
    }
}
