using System;

namespace Halibut.SampleContracts
{
    public interface IScriptOutputObserver
    {
        void LogWritten(string log);
    }
}