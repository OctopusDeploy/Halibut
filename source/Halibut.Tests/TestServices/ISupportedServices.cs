using System;
using System.Threading.Tasks;

namespace Halibut.Tests.TestServices
{
    public interface ISupportedServices
    {
        Task MethodReturningVoid(long a, long b);

        Task<long> Add(long a, long b);
        Task<double> Add(double a, double b);
        Task<decimal> Add(decimal a, decimal b);
        Task<string> Hello();
        Task<string> Hello(string a);
        Task<string> Hello(string a, string b);
        Task<string> Hello(string a, string b, string c);
        Task<string> Hello(string a, string b, string c, string d);
        Task<string> Hello(string a, string b, string c, string d, string e);
        Task<string> Hello(string a, string b, string c, string d, string e, string f);
        Task<string> Hello(string a, string b, string c, string d, string e, string f, string g);
        Task<string> Hello(string a, string b, string c, string d, string e, string f, string g, string h);
        Task<string> Hello(string a, string b, string c, string d, string e, string f, string g, string h, string i);
        Task<string> Hello(string a, string b, string c, string d, string e, string f, string g, string h, string i, string j);
        Task<string> Hello(string a, string b, string c, string d, string e, string f, string g, string h, string i, string j, string k);
        Task<string> Ambiguous(string a, string b);
        Task<string> Ambiguous(string a, Tuple<string, string> b);
    }
}