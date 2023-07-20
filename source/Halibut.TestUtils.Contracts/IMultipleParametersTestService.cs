using System;

namespace Halibut.TestUtils.Contracts
{

    public class MapLocation
    {
        public int Latitude { get; set; }
        
        public int Longitude { get; set; }
    }
    
    public interface IMultipleParametersTestService
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

        MapLocation GetLocation(MapLocation loc);
    }
}
