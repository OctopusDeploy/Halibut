using System;
using System.Text.RegularExpressions;

namespace Halibut.TestProxy
{
    record ProxyEndpoint(string Hostname, int Port)
    {
        static Regex ParseRegex = new Regex("(?<hostname>[\\S]+):(?<port>\\d+)", RegexOptions.Compiled);

        public override string ToString()
        {
            return $"{Hostname}:{Port}";
        }

        public static ProxyEndpoint Parse(string endpoint)
        {
            var match = ParseRegex.Match(endpoint);
            if (match.Success)
            {
                var hostname = match.Groups["hostname"].Value;

                return new ProxyEndpoint(match.Groups["hostname"].Value, int.Parse(match.Groups["port"].Value));
            }

            throw new ArgumentException($"Endpoint '{endpoint}' could not be parsed");
        }
    }
}