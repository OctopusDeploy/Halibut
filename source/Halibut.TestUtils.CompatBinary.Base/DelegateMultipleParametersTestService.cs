using System;
using Halibut.TestUtils.Contracts;
using Halibut.TestUtils.SampleProgram.Base.LogUtils;

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
            NonBlockingConsole.WriteLine("Forwarding MethodReturningVoid() call to delegate");
            multipleParametersTestService.MethodReturningVoid(a, b);
        }

        public long Add(long a, long b)
        {
            NonBlockingConsole.WriteLine("Forwarding Add(long, long) call to delegate");
            return multipleParametersTestService.Add(a, b);
        }

        public double Add(double a, double b)
        {
            NonBlockingConsole.WriteLine("Forwarding Add(double, double) call to delegate");
            return multipleParametersTestService.Add(a, b);
        }

        public decimal Add(decimal a, decimal b)
        {
            NonBlockingConsole.WriteLine("Forwarding Add(decimal, decimal) call to delegate");
            return multipleParametersTestService.Add(a, b);
        }

        public string Hello()
        {
            NonBlockingConsole.WriteLine("Forwarding Hello() call to delegate");
            return multipleParametersTestService.Hello();
        }

        public string Hello(string a)
        {
            NonBlockingConsole.WriteLine("Forwarding Hello(1 param) call to delegate");
            return multipleParametersTestService.Hello(a);
        }

        public string Hello(string a, string b)
        {
            NonBlockingConsole.WriteLine("Forwarding Hello(2 params) call to delegate");
            return multipleParametersTestService.Hello(a, b);
        }

        public string Hello(string a, string b, string c)
        {
            NonBlockingConsole.WriteLine("Forwarding Hello(3 params) call to delegate");
            return multipleParametersTestService.Hello(a, b, c);
        }

        public string Hello(string a, string b, string c, string d)
        {
            NonBlockingConsole.WriteLine("Forwarding Hello(4 params) call to delegate");
            return multipleParametersTestService.Hello(a, b, c, d);
        }

        public string Hello(string a, string b, string c, string d, string e)
        {
            NonBlockingConsole.WriteLine("Forwarding Hello(5 params) call to delegate");
            return multipleParametersTestService.Hello(a, b, c, d, e);
        }

        public string Hello(string a, string b, string c, string d, string e, string f)
        {
            NonBlockingConsole.WriteLine("Forwarding Hello(6 params) call to delegate");
            return multipleParametersTestService.Hello(a, b, c, d, e, f);
        }

        public string Hello(string a, string b, string c, string d, string e, string f, string g)
        {
            NonBlockingConsole.WriteLine("Forwarding Hello(7 params) call to delegate");
            return multipleParametersTestService.Hello(a, b, c, d, e, f, g);
        }

        public string Hello(string a, string b, string c, string d, string e, string f, string g, string h)
        {
            NonBlockingConsole.WriteLine("Forwarding Hello(8 params) call to delegate");
            return multipleParametersTestService.Hello(a, b, c, d, e, f, g, h);
        }

        public string Hello(string a, string b, string c, string d, string e, string f, string g, string h, string i)
        {
            NonBlockingConsole.WriteLine("Forwarding Hello(9 params) call to delegate");
            return multipleParametersTestService.Hello(a, b, c, d, e, f, g, h, i);
        }

        public string Hello(string a, string b, string c, string d, string e, string f, string g, string h, string i, string j)
        {
            NonBlockingConsole.WriteLine("Forwarding Hello(10 params) call to delegate");
            return multipleParametersTestService.Hello(a, b, c, d, e, f, g, h, i, j);
        }

        public string Hello(string a, string b, string c, string d, string e, string f, string g, string h, string i, string j, string k)
        {
            NonBlockingConsole.WriteLine("Forwarding Hello(11 params) call to delegate");
            return multipleParametersTestService.Hello(a, b, c, d, e, f, g, h, i, j, k);
        }

        public string Ambiguous(string a, string b)
        {
            NonBlockingConsole.WriteLine("Forwarding Ambiguous(string, string) call to delegate");
            return multipleParametersTestService.Ambiguous(a, b);
        }

        public string Ambiguous(string a, Tuple<string, string> b)
        {
            NonBlockingConsole.WriteLine("Forwarding Ambiguous(string, Tuple) call to delegate");
            return multipleParametersTestService.Ambiguous(a, b);
        }

        public MapLocation GetLocation(MapLocation loc)
        {
            NonBlockingConsole.WriteLine("Forwarding GetLocation() call to delegate");
            return multipleParametersTestService.GetLocation(loc);
        }
    }
}