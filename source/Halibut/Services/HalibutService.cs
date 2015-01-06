using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Halibut.Services
{
    public class Server
    {
        // Accepts TCP connections
        // Parses the first line. If it looks like a HTTP request, returns HTTP+HTML page
        // Otherwise, looks for a special header:
        // <<mx/v1>>  -- message exchange
        // <<stream/v1>> -- streams
    }
}