using System;
using Halibut;
using Newtonsoft.Json;

namespace Octopus.Tentacle.Contracts
{
    public class ScriptFile
    {
        [JsonConstructor]
        public ScriptFile(string name, DataStream contents, string? encryptionPassword)
        {
            Name = name;
            Contents = contents;
            EncryptionPassword = encryptionPassword;
        }

        public ScriptFile(string name, DataStream contents) : this(name, contents, null)
        {
        }

        public string Name { get; }

        public DataStream Contents { get; }

        public string? EncryptionPassword { get; }
    }
}