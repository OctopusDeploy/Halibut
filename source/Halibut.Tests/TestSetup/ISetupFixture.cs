using Serilog;

namespace Halibut.Tests.TestSetup
{
    public interface ISetupFixture
    {
        public void OneTimeSetUp(ILogger logger);
        
        public void OneTimeTearDown(ILogger logger);
    }
}