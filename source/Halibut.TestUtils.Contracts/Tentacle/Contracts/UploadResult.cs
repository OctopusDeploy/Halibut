using System;

namespace Octopus.Tentacle.Contracts
{
    public class UploadResult
    {
        public UploadResult(string fullPath, string hash, long length)
        {
            FullPath = fullPath;
            Hash = hash;
            Length = length;
        }

        public string FullPath { get; }

        public string Hash { get; }

        public long Length { get; }
    }
}