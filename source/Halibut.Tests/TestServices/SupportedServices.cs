using System;

namespace Halibut.Tests.TestServices
{
    public class SupportedServices : ISupportedServices
    {
        public void MethodReturningVoid(long a, long b)
        {
        }

        public long Add(long a, long b)
        {
            return a + b;
        }

        public double Add(double a, double b)
        {
            return a + b;
        }

        public decimal Add(decimal a, decimal b)
        {
            return a + b;
        }

        public string Hello()
        {
            return "Hello";
        }

        public string Hello(string a)
        {
            return "Hello " + a;
        }

        public string Hello(string a, string b)
        {
            return "Hello " + string.Join(" ", a, b);
        }

        public string Hello(string a, string b, string c)
        {
            return "Hello " + string.Join(" ", a, b, c);
        }

        public string Hello(string a, string b, string c, string d)
        {
            return "Hello " + string.Join(" ", a, b, c, d);
        }

        public string Hello(string a, string b, string c, string d, string e)
        {
            return "Hello " + string.Join(" ", a, b, c, d, e);
        }

        public string Hello(string a, string b, string c, string d, string e, string f)
        {
            return "Hello " + string.Join(" ", a, b, c, d, e, f);
        }

        public string Hello(string a, string b, string c, string d, string e, string f, string g)
        {
            return "Hello " + string.Join(" ", a, b, c, d, e, f, g);
        }

        public string Hello(string a, string b, string c, string d, string e, string f, string g, string h)
        {
            return "Hello " + string.Join(" ", a, b, c, d, e, f, g, h);
        }

        public string Hello(string a, string b, string c, string d, string e, string f, string g, string h, string i)
        {
            return "Hello " + string.Join(" ", a, b, c, d, e, f, g, h, i);
        }

        public string Hello(string a, string b, string c, string d, string e, string f, string g, string h, string i, string j)
        {
            return "Hello " + string.Join(" ", a, b, c, d, e, f, g, h, i, j);
        }

        public string Hello(string a, string b, string c, string d, string e, string f, string g, string h, string i, string j, string k)
        {
            return "Hello " + string.Join(" ", a, b, c, d, e, f, g, h, i, j, k);
        }

        public string Ambiguous(string a, string b)
        {
            return "Hello string";
        }

        public string Ambiguous(string a, Tuple<string, string> b)
        {
            return "Hello tuple";
        }
    }
}
