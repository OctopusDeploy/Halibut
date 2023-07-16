using System;
using System.Diagnostics;
using NUnit.Framework;

namespace Halibut.Tests.Support.TestAttributes
{
    public class TestTimeoutAttribute : TimeoutAttribute
    {
        public TestTimeoutAttribute() : base(TestTimeout())
        {
        }

        public static int TestTimeout()
        {
            if (Debugger.IsAttached) return (int) TimeSpan.FromHours(1).TotalMilliseconds;
            return (int) TimeSpan.FromMinutes(6).TotalMilliseconds;
        }
    }
}