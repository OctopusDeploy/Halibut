using System;

namespace Halibut.SampleContracts
{
    public interface IScriptRunner
    {
        int RunScript(string scriptName, object[] args);
    }
}