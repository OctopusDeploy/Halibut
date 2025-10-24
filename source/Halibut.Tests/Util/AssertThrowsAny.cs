using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Halibut.Tests.Util
{
    public static class AssertThrowsAny
    {
        public static async Task<Exception> Exception(Func<Task> action)
        {
            try
            {
                await action();
                Assert.Fail("Should have thrown an exception.");
            }
            catch (Exception exception)
            {
                return exception;
            }

            throw new Exception("Impossible?");
        }
    }
}