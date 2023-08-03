using System;
using System.Diagnostics;
using NUnit.Framework;

namespace Halibut.Tests.Support.TestAttributes
{
    public class TestTimeoutAttribute : TimeoutAttribute
    {
        public TestTimeoutAttribute(int timeoutInSeconds) : base((int)TimeSpan.FromSeconds(timeoutInSeconds).TotalMilliseconds)
        {
        }

        public TestTimeoutAttribute() : base(TestTimeoutInMilliseconds())
        {
        }

        public static int TestTimeoutInMilliseconds()
        {
            if (Debugger.IsAttached) return (int) TimeSpan.FromHours(1).TotalMilliseconds;
            return (int) TimeSpan.FromMinutes(6).TotalMilliseconds;
        }
    }
}