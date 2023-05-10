namespace Halibut.Tests.Util;

public class NullPortForwarder : IPortForwarder
{
    public NullPortForwarder(int listeningPort)
    {
        ListeningPort = listeningPort;
    }

    public void Dispose()
    {
    }

    public int ListeningPort { get; }
}