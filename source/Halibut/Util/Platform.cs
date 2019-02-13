using System;

namespace Halibut.Util
{
    internal static class Platform
    {
        internal static bool IsRunningOnWindows => Environment.OSVersion.Platform == PlatformID.Win32NT ||
                                                 Environment.OSVersion.Platform == PlatformID.Win32S ||
                                                 Environment.OSVersion.Platform == PlatformID.Win32Windows ||
                                                 Environment.OSVersion.Platform == PlatformID.WinCE;
    }
}