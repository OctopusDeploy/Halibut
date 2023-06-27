using System;

namespace Halibut.Transport.Caching
{
    public class CacheResponseAttribute : Attribute
    {
        public CacheResponseAttribute(int durationInSeconds)
        {
            DurationInSeconds = durationInSeconds;
        }

        public int DurationInSeconds { get; set; }
    }
}