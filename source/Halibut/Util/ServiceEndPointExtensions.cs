namespace Halibut.Util
{
    static class ServiceEndPointExtensions
    {
        public static string Format(this ServiceEndPoint serviceEndpoint)
            => serviceEndpoint?.BaseUri.ToString() ?? "(Null EndPoint)";
    }
}