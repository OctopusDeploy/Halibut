namespace Halibut.Tests.TestServices
{
    public interface IReadDataSteamService
    {
        long SendData(DataStream dataStream);

        long SendDataMany(DataStream[] dataStreams);
    }
}