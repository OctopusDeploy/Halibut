using System;
using Xunit;

namespace Halibut.Tests.Util
{
    // CollectionDefinition does NOT WORK when put on a test class itself. So we make this separate collection to use CollectionDefinition.
    [CollectionDefinition(nameof(DoNotRunInParallelGlobally), DisableParallelization = true)]
    public class DoNotRunInParallelGlobally
    {
    }
}