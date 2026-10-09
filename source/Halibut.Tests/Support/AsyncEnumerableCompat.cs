using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Halibut.Tests.Support
{
    // On net10.0 the framework's System.Linq.AsyncEnumerable and the System.Linq.Async package
    // (pulled in by System.Interactive.Async on net48) both define ToAsyncEnumerable and SelectAwait-style
    // operators, which makes calls ambiguous or unavailable. These helpers give both targets one implementation.
    public static class AsyncEnumerableCompat
    {
        public static async IAsyncEnumerable<T> ToAsyncEnumerableCompat<T>(this IEnumerable<T> source)
        {
            foreach (var item in source)
            {
                yield return item;
            }
        }

        public static async IAsyncEnumerable<TResult> SelectAwaitCompat<TSource, TResult>(this IAsyncEnumerable<TSource> source, Func<TSource, Task<TResult>> selector)
        {
            await foreach (var item in source)
            {
                yield return await selector(item);
            }
        }
    }
}
