using System.Collections;
using System.Collections.Generic;

namespace Halibut.Tests.Util
{
    public static class EnumerableExtensionMethods
    {
        public static object[] ToArrayOfObjects(this IEnumerable enumerable)
        {
            var enumerator = enumerable.GetEnumerator();
            var list = new List<object>();

            while (enumerator.MoveNext())
            {
                list.Add(enumerator.Current);
            }

            return list.ToArray();
        }
    }
}