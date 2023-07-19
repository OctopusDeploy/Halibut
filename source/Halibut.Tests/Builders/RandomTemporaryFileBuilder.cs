using System;
using System.IO;

namespace Halibut.Tests.Builders
{
    internal class RandomTemporaryFileBuilder
    {
        private int sizeInMb = 2;

        public RandomTemporaryFileBuilder WithSizeInMb(int sizeInMb)
        {
            this.sizeInMb = sizeInMb;

            return this;
        }

        public RandomTemporaryFile Build()
        {
            var tempFile = Path.GetTempFileName();
            var data = new byte[sizeInMb * 1024 * 1024];
            var rng = new Random();
            rng.NextBytes(data);
            File.WriteAllBytes(tempFile, data);
            return new RandomTemporaryFile(new FileInfo(tempFile));
        }
    }

    internal class RandomTemporaryFile : IDisposable
    {
        public FileInfo File { get; }

        public RandomTemporaryFile(FileInfo file)
        {
            File = file;
        }

        public void Dispose()
        {
            if (File.Exists)
            {
                File.Delete();
            }
        }
    }
}