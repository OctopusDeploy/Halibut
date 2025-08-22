using System;

namespace Halibut.Util
{
    public static class TimeSpanHelper
    {
        public static TimeSpan Min(TimeSpan t1, TimeSpan t2)
        {
            return t1 < t2 ? t1 : t2;
        }
    }
}