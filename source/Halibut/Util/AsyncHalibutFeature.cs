namespace Halibut.Util
{
    public enum AsyncHalibutFeature
    {
        Enabled,
        Disabled    
    }

    public static class AsyncHalibutFeatureExtensionMethods
    {
        public static bool IsEnabled(this AsyncHalibutFeature feature)
        {
            return feature == AsyncHalibutFeature.Enabled;
        }

        public static bool IsDisabled(this AsyncHalibutFeature feature)
        {
            return feature == AsyncHalibutFeature.Disabled;
        }
    }
}
