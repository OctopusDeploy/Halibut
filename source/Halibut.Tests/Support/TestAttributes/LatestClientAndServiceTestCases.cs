using System;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.Transport.Protocol;

namespace Halibut.Tests.Support.TestAttributes
{
    /// <summary>
    ///     Holds all the standard test cases for testing a latest client with a latest service.
    ///     In this case latest means the code as is, rather than some previously built version of halibut.
    /// </summary>
    public class LatestClientAndServiceTestCases : ClientAndServiceTestCases
    {
        public LatestClientAndServiceTestCases()
            : base(ClientServiceTestVersion.Latest())
        {
        }
    }
}
