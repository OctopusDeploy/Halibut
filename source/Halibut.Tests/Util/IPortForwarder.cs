using System;

namespace Halibut.Tests.Util
{
    public interface IPortForwarder : IDisposable
    {
        public int ListeningPort { get; }
    }
}