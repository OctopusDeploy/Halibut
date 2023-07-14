using System.Collections;

namespace Halibut.Tests.Support.TestCases
{
    public class AsyncTestCase : IEnumerable
    {
        public IEnumerator GetEnumerator()
        {
            yield return true;
            yield return false;
        }
    }
}