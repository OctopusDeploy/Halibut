using System;
using Halibut.TestUtils.SampleProgram.Base.Services;

namespace Halibut.TestUtils.SampleProgram.Base
{
    public class DelegateMultipleParametersTestService : IMultipleParametersTestService
    {
        readonly IMultipleParametersTestService multipleParametersTestService;

        public DelegateMultipleParametersTestService(IMultipleParametersTestService multipleParametersTestService)
        {
            this.multipleParametersTestService = multipleParametersTestService;
        }

        public void MethodReturningVoid(long a, long b)
        {
            Console.WriteLine("Forwarding MethodReturningVoid() call to delegate");
            multipleParametersTestService.MethodReturningVoid(a, b);
        }

        public long Add(long a, long b)
        {
            Console.WriteLine("Forwarding Add(long, long) call to delegate");
            return multipleParametersTestService.Add(a, b);
        }

        public double Add(double a, double b)
        {
            Console.WriteLine("Forwarding Add(double, double) call to delegate");
            return multipleParametersTestService.Add(a, b);
        }

        public decimal Add(decimal a, decimal b)
        {
            Console.WriteLine("Forwarding Add(decimal, decimal) call to delegate");
            return multipleParametersTestService.Add(a, b);
        }

        public string Hello()
        {
            Console.WriteLine("Forwarding Hello() call to delegate");
            return multipleParametersTestService.Hello();
        }

        public string Hello(string a)
        {
            Console.WriteLine("Forwarding Hello(1 param) call to delegate");
            return multipleParametersTestService.Hello(a);
        }

        public string Hello(string a, string b)
        {
            Console.WriteLine("Forwarding Hello(2 params) call to delegate");
            return multipleParametersTestService.Hello(a, b);
        }

        public string Hello(string a, string b, string c)
        {
            Console.WriteLine("Forwarding Hello(3 params) call to delegate");
            return multipleParametersTestService.Hello(a, b, c);
        }

        public string Hello(string a, string b, string c, string d)
        {
            Console.WriteLine("Forwarding Hello(4 params) call to delegate");
            return multipleParametersTestService.Hello(a, b, c, d);
        }

        public string Hello(string a, string b, string c, string d, string e)
        {
            Console.WriteLine("Forwarding Hello(5 params) call to delegate");
            return multipleParametersTestService.Hello(a, b, c, d, e);
        }

        public string Hello(string a, string b, string c, string d, string e, string f)
        {
            Console.WriteLine("Forwarding Hello(6 params) call to delegate");
            return multipleParametersTestService.Hello(a, b, c, d, e, f);
        }

        public string Hello(string a, string b, string c, string d, string e, string f, string g)
        {
            Console.WriteLine("Forwarding Hello(7 params) call to delegate");
            return multipleParametersTestService.Hello(a, b, c, d, e, f, g);
        }

        public string Hello(string a, string b, string c, string d, string e, string f, string g, string h)
        {
            Console.WriteLine("Forwarding Hello(8 params) call to delegate");
            return multipleParametersTestService.Hello(a, b, c, d, e, f, g, h);
        }

        public string Hello(string a, string b, string c, string d, string e, string f, string g, string h, string i)
        {
            Console.WriteLine("Forwarding Hello(9 params) call to delegate");
            return multipleParametersTestService.Hello(a, b, c, d, e, f, g, h, i);
        }

        public string Hello(string a, string b, string c, string d, string e, string f, string g, string h, string i, string j)
        {
            Console.WriteLine("Forwarding Hello(10 params) call to delegate");
            return multipleParametersTestService.Hello(a, b, c, d, e, f, g, h, i, j);
        }

        public string Hello(string a, string b, string c, string d, string e, string f, string g, string h, string i, string j, string k)
        {
            Console.WriteLine("Forwarding Hello(11 params) call to delegate");
            return multipleParametersTestService.Hello(a, b, c, d, e, f, g, h, i, j, k);
        }

        public string Ambiguous(string a, string b)
        {
            Console.WriteLine("Forwarding Ambiguous(string, string) call to delegate");
            return multipleParametersTestService.Ambiguous(a, b);
        }

        public string Ambiguous(string a, Tuple<string, string> b)
        {
            Console.WriteLine("Forwarding Ambiguous(string, Tuple) call to delegate");
            return multipleParametersTestService.Ambiguous(a, b);
        }

        public MapLocation GetLocation(MapLocation loc)
        {
            Console.WriteLine("Forwarding GetLocation() call to delegate");
            return multipleParametersTestService.GetLocation(loc);
        }
    }
}
