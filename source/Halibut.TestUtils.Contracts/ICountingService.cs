namespace Halibut.TestUtils.Contracts
{
    public interface ICountingService
    {
        public int Increment();
        public int GetCurrentValue();
    }
}