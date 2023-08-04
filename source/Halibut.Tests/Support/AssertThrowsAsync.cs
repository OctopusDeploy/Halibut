using System;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Specialized;

namespace Halibut.Tests.Support
{
    public static class AssertAsync
    {
        public static async Task<ExceptionAssertions<T>> Throws<T>(this Func<Task> task) where T : Exception
        {
            return await task.Should().ThrowAsync<T>();
        }
    }
}