using System;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Specialized;

namespace Halibut.Tests.Support
{
    public static class AssertException
    {
        public static async Task<ExceptionAssertions<T>> Throws<T>(this Func<Task> task, string because = "") where T : Exception
        {
            return await task.Should().ThrowAsync<T>(because);
        }

        public static async Task<ExceptionAssertions<TExpectedException>> Throws<TExpectedException>(Task task, string because = "") where TExpectedException : Exception
        {
#pragma warning disable VSTHRD003 // Avoid awaiting foreign Tasks
            return await AssertionExtensions.Should(() => task).ThrowAsync<TExpectedException>(because);
#pragma warning restore VSTHRD003 // Avoid awaiting foreign Tasks
        }

        public static ExceptionAssertions<T> Throws<T>(this Action task, string because = "") where T : Exception
        {
            return task.Should().Throw<T>(because);
        }
    }
}