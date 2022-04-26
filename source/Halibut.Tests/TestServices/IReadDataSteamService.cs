namespace Halibut.Tests.TestServices
{
    public interface IReadDataSteamService
    {
        long SendData(params DataStream[] dataStreams);
    }
}