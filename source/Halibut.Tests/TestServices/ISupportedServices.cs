using System;

namespace Halibut.Tests.TestServices
{
    public interface ISupportedServices
    {
        void MethodReturningVoid(long a, long b);

        long Add(long a, long b);
        double Add(double a, double b);
        decimal Add(decimal a, decimal b);

        string Hello();
        string Hello(string a);
        string Hello(string a, string b);
        string Hello(string a, string b, string c);
        string Hello(string a, string b, string c, string d);
        string Hello(string a, string b, string c, string d, string e);
        string Hello(string a, string b, string c, string d, string e, string f);
        string Hello(string a, string b, string c, string d, string e, string f, string g);
        string Hello(string a, string b, string c, string d, string e, string f, string g, string h);
        string Hello(string a, string b, string c, string d, string e, string f, string g, string h, string i);
        string Hello(string a, string b, string c, string d, string e, string f, string g, string h, string i, string j);
        string Hello(string a, string b, string c, string d, string e, string f, string g, string h, string i, string j, string k);

        string Ambiguous(string a, string b);
        string Ambiguous(string a, Tuple<string, string> b);
    }
}