namespace Halibut.Tests.TestServices
{
    public interface IReadDataStreamService
    {
        long SendData(params DataStream[] dataStreams);
    }
}