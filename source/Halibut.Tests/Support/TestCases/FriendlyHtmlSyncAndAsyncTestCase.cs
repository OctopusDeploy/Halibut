using System;
using Halibut.Tests.Support.TestAttributes;

namespace Halibut.Tests.Support.TestCases
{
    public class FriendlyHtmlSyncAndAsyncTestCase
    {
        public SyncOrAsync SyncOrAsync;
        public string? Html;
        public string? Expected;

        public override string ToString()
        {
            string htmlDisplay = Html switch
            {
                null => "<null>",
                "" => "<empty>",
                _ => Html
            };

            return $"{SyncOrAsync}, Html: {htmlDisplay}, Expected: {Expected ?? "<null>"}";
        }
    }
}
