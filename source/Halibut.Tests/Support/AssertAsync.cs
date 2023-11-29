using System;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Specialized;

namespace Halibut.Tests.Support
{
    public static class AssertAsync
    {
        public static async Task<ExceptionAssertions<T>> Throws<T>(this Func<Task> task, string because = "") where T : Exception
        {
            return await task.Should().ThrowAsync<T>(because);
        }

        //TODO: @server-at-scale this does not belong here. Move it, or rename this class.
        public static ExceptionAssertions<T> Throws<T>(this Action task, string because = "") where T : Exception
        {
            return task.Should().Throw<T>(because);
        }
    }
}