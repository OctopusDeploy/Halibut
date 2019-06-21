using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Halibut
{
    public class DataStream : IEquatable<Halibut.DataStream>
    {
        public DataStream() { }
        public DataStream(long length, Action<Stream> writer) { }
        public Guid Id { get; set; }
        public long Length { get; set; }
        public bool Equals(Halibut.DataStream other) { }
        public bool Equals(Object obj) { }
        public static Halibut.DataStream FromBytes(Byte[] data) { }
        public static Halibut.DataStream FromStream(Stream source, Action<int> updateProgress) { }
        public static Halibut.DataStream FromStream(Stream source) { }
        public static Halibut.DataStream FromString(string text) { }
        public static Halibut.DataStream FromString(string text, Encoding encoding) { }
        public int GetHashCode() { }
        public Halibut.IDataStreamReceiver Receiver() { }
    }
    public class HalibutClientException : Exception, ISerializable
    {
        public HalibutClientException(string message) { }
        public HalibutClientException(string message, Exception inner) { }
        public HalibutClientException(string message, string serverException) { }
    }
    public class HalibutRuntime : Halibut.IHalibutRuntime, IDisposable
    {
        public static string DefaultFriendlyHtmlPageContent;
        public HalibutRuntime(X509Certificate2 serverCertificate) { }
        public HalibutRuntime(X509Certificate2 serverCertificate, Halibut.ServiceModel.ITrustProvider trustProvider) { }
        public HalibutRuntime(Halibut.ServiceModel.IServiceFactory serviceFactory, X509Certificate2 serverCertificate) { }
        public HalibutRuntime(Halibut.ServiceModel.IServiceFactory serviceFactory, X509Certificate2 serverCertificate, Halibut.ServiceModel.ITrustProvider trustProvider) { }
        public Halibut.Diagnostics.ILogFactory Logs { get; }
        public Func<string, string, Halibut.UnauthorizedClientConnectResponse> OnUnauthorizedClientConnect { get; set; }
        public static bool OSSupportsWebSockets { get; }
        public TService CreateClient<TService>(string endpointBaseUri, string publicThumbprint) { }
        public TService CreateClient<TService>(Halibut.ServiceEndPoint endpoint) { }
        public void Disconnect(Halibut.ServiceEndPoint endpoint) { }
        public Halibut.ServiceEndPoint Discover(Uri uri) { }
        public Halibut.ServiceEndPoint Discover(Halibut.ServiceEndPoint endpoint) { }
        public void Dispose() { }
        protected Halibut.UnauthorizedClientConnectResponse HandleUnauthorizedClientConnect(string clientName, string thumbPrint) { }
        public bool IsTrusted(string remoteThumbprint) { }
        public int Listen() { }
        public int Listen(int port) { }
        public int Listen(IPEndPoint endpoint) { }
        public void ListenWebSocket(string endpoint) { }
        public void Poll(Uri subscription, Halibut.ServiceEndPoint endPoint) { }
        public void RemoveTrust(string clientThumbprint) { }
        public void Route(Halibut.ServiceEndPoint to, Halibut.ServiceEndPoint via) { }
        public void SetFriendlyHtmlPageContent(string html) { }
        public void SetFriendlyHtmlPageHeaders(IEnumerable<KeyValuePair<string, string>> headers) { }
        public void Trust(string clientThumbprint) { }
        public void TrustOnly(IReadOnlyList<string> thumbprints) { }
    }
    public interface IDataStreamReceiver
    {
        public void Read(Action<Stream> reader) { }
        public void SaveTo(string filePath) { }
    }
    public interface IHalibutRuntime : IDisposable
    {
        public Halibut.Diagnostics.ILogFactory Logs { get; }
        public Func<string, string, Halibut.UnauthorizedClientConnectResponse> OnUnauthorizedClientConnect { get; set; }
        public TService CreateClient<TService>(string endpointBaseUri, string publicThumbprint) { }
        public TService CreateClient<TService>(Halibut.ServiceEndPoint endpoint) { }
        public void Disconnect(Halibut.ServiceEndPoint endpoint) { }
        public Halibut.ServiceEndPoint Discover(Uri uri) { }
        public Halibut.ServiceEndPoint Discover(Halibut.ServiceEndPoint endpoint) { }
        public bool IsTrusted(string remoteThumbprint) { }
        public int Listen() { }
        public int Listen(int port) { }
        public int Listen(IPEndPoint endpoint) { }
        public void ListenWebSocket(string endpoint) { }
        public void Poll(Uri subscription, Halibut.ServiceEndPoint endPoint) { }
        public void RemoveTrust(string clientThumbprint) { }
        public void Route(Halibut.ServiceEndPoint to, Halibut.ServiceEndPoint via) { }
        public void SetFriendlyHtmlPageContent(string html) { }
        public void Trust(string clientThumbprint) { }
        public void TrustOnly(IReadOnlyList<string> thumbprints) { }
    }
    public class ProxyDetails : IEquatable<Halibut.ProxyDetails>
    {
        public ProxyDetails(string host, int port, Halibut.Transport.Proxy.ProxyType type) { }
        public ProxyDetails(string host, int port, Halibut.Transport.Proxy.ProxyType type, string userName, string password) { }
        public string Host { get; }
        public string Password { get; }
        public int Port { get; }
        public Halibut.Transport.Proxy.ProxyType Type { get; }
        public string UserName { get; }
        public bool Equals(Halibut.ProxyDetails other) { }
        public bool Equals(Object obj) { }
        public int GetHashCode() { }
    }
    public class ServiceEndPoint : IEquatable<Halibut.ServiceEndPoint>
    {
        public ServiceEndPoint(string baseUri, string remoteThumbprint) { }
        public ServiceEndPoint(Uri baseUri, string remoteThumbprint) { }
        public ServiceEndPoint(Uri baseUri, string remoteThumbprint, Halibut.ProxyDetails proxy) { }
        public Uri BaseUri { get; }
        public TimeSpan ConnectionErrorRetryTimeout { get; set; }
        public bool IsWebSocketEndpoint { get; }
        public TimeSpan PollingRequestMaximumMessageProcessingTimeout { get; set; }
        public TimeSpan PollingRequestQueueTimeout { get; set; }
        public Halibut.ProxyDetails Proxy { get; }
        public string RemoteThumbprint { get; }
        public int RetryCountLimit { get; set; }
        public TimeSpan RetryListeningSleepInterval { get; set; }
        public TimeSpan TcpClientConnectTimeout { get; set; }
        public bool Equals(Halibut.ServiceEndPoint other) { }
        public bool Equals(Object obj) { }
        public int GetHashCode() { }
        public static bool IsWebSocketAddress(Uri baseUri) { }
        public string ToString() { }
    }
    public enum UnauthorizedClientConnectResponse
    {
        BlockConnection = 0,
        TrustAndAllowConnection = 1
    }
}
namespace Halibut.Diagnostics
{
    public enum EventType
    {
        OpeningNewConnection = 0,
        UsingExistingConnectionFromPool = 1,
        Security = 2,
        MessageExchange = 3,
        Diagnostic = 4,
        ClientDenied = 5,
        Error = 6,
        ListenerStarted = 7,
        ListenerAcceptedClient = 8,
        ListenerStopped = 9,
        SecurityNegotiation = 10,
        FileTransfer = 11
    }
    public static class ExceptionExtensions
    {
        public static bool IsSocketConnectionReset(Exception exception) { }
        public static bool IsSocketConnectionTimeout(Exception exception) { }
        public static bool IsSocketTimeout(Exception exception) { }
        public static Exception UnpackFromContainers(Exception error) { }
    }
    public class HalibutLimits
    {
        public static TimeSpan ConnectionErrorRetryTimeout;
        public static TimeSpan PollingQueueWaitTimeout;
        public static TimeSpan PollingRequestMaximumMessageProcessingTimeout;
        public static TimeSpan PollingRequestQueueTimeout;
        public static int RetryCountLimit;
        public static TimeSpan RetryListeningSleepInterval;
        public static TimeSpan TcpClientConnectTimeout;
        public static TimeSpan TcpClientHeartbeatReceiveTimeout;
        public static TimeSpan TcpClientHeartbeatSendTimeout;
        public static TimeSpan TcpClientPooledConnectionTimeout;
        public static TimeSpan TcpClientReceiveTimeout;
        public static TimeSpan TcpClientSendTimeout;
        public HalibutLimits() { }
        public static TimeSpan SafeTcpClientPooledConnectionTimeout { get; }
    }
    public interface ILog
    {
        public IList<Halibut.Diagnostics.LogEvent> GetLogs() { }
        public void Write(Halibut.Diagnostics.EventType type, string message, Object[] args) { }
        public void WriteException(Halibut.Diagnostics.EventType type, string message, Exception ex, Object[] args) { }
    }
    public interface ILogFactory
    {
        public Halibut.Diagnostics.ILog ForEndpoint(Uri endpoint) { }
        public Halibut.Diagnostics.ILog ForPrefix(string endPoint) { }
        public Uri[] GetEndpoints() { }
        public String[] GetPrefixes() { }
    }
    public class LogEvent
    {
        public LogEvent(Halibut.Diagnostics.EventType type, string message, Exception error, Object[] formatArguments) { }
        public Exception Error { get; }
        public string FormattedMessage { get; }
        public string Message { get; }
        public DateTimeOffset Time { get; }
        public Halibut.Diagnostics.EventType Type { get; }
        public string ToString() { }
    }
    public class LogFactory : Halibut.Diagnostics.ILogFactory
    {
        public LogFactory() { }
        public Halibut.Diagnostics.ILog ForEndpoint(Uri endpoint) { }
        public Halibut.Diagnostics.ILog ForPrefix(string prefix) { }
        public Uri[] GetEndpoints() { }
        public String[] GetPrefixes() { }
    }
}
namespace Halibut.Logging
{
    public interface ILogProvider
    {
        public Halibut.Logging.Logger GetLogger(string name) { }
        public IDisposable OpenMappedContext(string key, string value) { }
        public IDisposable OpenNestedContext(string message) { }
    }
    public sealed class Logger : MulticastDelegate, ICloneable, ISerializable
    {
        public Logger(Object @object, IntPtr method) { }
        public IAsyncResult BeginInvoke(Halibut.Logging.LogLevel logLevel, Func<string> messageFunc, Exception exception, Object[] formatParameters, AsyncCallback callback, Object @object) { }
        public bool EndInvoke(IAsyncResult result) { }
        public bool Invoke(Halibut.Logging.LogLevel logLevel, Func<string> messageFunc, Exception exception, Object[] formatParameters) { }
    }
    public enum LogLevel
    {
        Trace = 0,
        Debug = 1,
        Info = 2,
        Warn = 3,
        Error = 4,
        Fatal = 5
    }
    public static class LogProvider
    {
        public static string DisableLoggingEnvironmentVariable;
        internal static Halibut.Logging.ILogProvider CurrentLogProvider {  }
        public static bool IsDisabled { get; set; }
        static Action<Halibut.Logging.ILogProvider> OnCurrentLogProviderSet {  }
        public static void SetCurrentLogProvider(Halibut.Logging.ILogProvider logProvider) { }
    }
}
namespace Halibut.ServiceModel
{
    public class DelegateServiceFactory : Halibut.ServiceModel.IServiceFactory
    {
        public DelegateServiceFactory() { }
        public Halibut.ServiceModel.IServiceLease CreateService(string serviceName) { }
        public void Register<TContract>(Func<TContract> implementation) { }
    }
    public class HalibutProxy : DispatchProxy
    {
        public HalibutProxy() { }
        public void Configure(Func<Halibut.Transport.Protocol.RequestMessage, Halibut.Transport.Protocol.ResponseMessage> messageRouter, Type contractType, Halibut.ServiceEndPoint endPoint) { }
        protected Object Invoke(MethodInfo targetMethod, Object[] args) { }
    }
    public interface IServiceFactory
    {
        public Halibut.ServiceModel.IServiceLease CreateService(string serviceName) { }
    }
    public interface IServiceInvoker
    {
        public Halibut.Transport.Protocol.ResponseMessage Invoke(Halibut.Transport.Protocol.RequestMessage requestMessage) { }
    }
    public interface IServiceLease : IDisposable
    {
        public Object Service { get; }
    }
    public interface ITrustProvider
    {
        public void Add(string clientThumbprint) { }
        public bool IsTrusted(string clientThumbprint) { }
        public void Remove(string clientThumbprint) { }
        public String[] ToArray() { }
        public void TrustOnly(IReadOnlyList<string> thumbprints) { }
    }
    public class NullServiceFactory : Halibut.ServiceModel.IServiceFactory
    {
        public NullServiceFactory() { }
        public Halibut.ServiceModel.IServiceLease CreateService(string serviceName) { }
    }
    public class ServiceInvoker : Halibut.ServiceModel.IServiceInvoker
    {
        public ServiceInvoker(Halibut.ServiceModel.IServiceFactory factory) { }
        public Halibut.Transport.Protocol.ResponseMessage Invoke(Halibut.Transport.Protocol.RequestMessage requestMessage) { }
    }
}
namespace Halibut.Transport
{
    public class ConnectionPool<TKey, TPooledResource>
    {
        public ConnectionPool() { }
        public void Clear(TKey key, Halibut.Diagnostics.ILog log) { }
        public void Dispose() { }
        public int GetTotalConnectionCount() { }
        public void Return(TKey endPoint, TPooledResource resource) { }
        public TPooledResource Take(TKey endPoint) { }
    }
    public class DiscoveryClient
    {
        public DiscoveryClient() { }
        public Halibut.ServiceEndPoint Discover(Halibut.ServiceEndPoint serviceEndpoint) { }
    }
    public interface IPooledResource : IDisposable
    {
        public bool HasExpired() { }
        public void NotifyUsed() { }
    }
}
namespace Halibut.Transport.Protocol
{
    public class ConnectionInitializationFailedException : Exception, ISerializable
    {
        public ConnectionInitializationFailedException(string message) { }
        public ConnectionInitializationFailedException(string message, Exception innerException) { }
        public ConnectionInitializationFailedException(Exception innerException) { }
    }
    public class HalibutContractResolver : Newtonsoft.Json.Serialization.DefaultContractResolver, Newtonsoft.Json.Serialization.IContractResolver
    {
        public HalibutContractResolver() { }
        public Newtonsoft.Json.Serialization.JsonContract ResolveContract(Type type) { }
    }
    public class InMemoryDataStreamReceiver : Halibut.IDataStreamReceiver
    {
        public InMemoryDataStreamReceiver(Action<Stream> writer) { }
        public void Read(Action<Stream> reader) { }
        public void SaveTo(string filePath) { }
    }
    public class ProtocolException : Exception, ISerializable
    {
        public ProtocolException(string message) { }
    }
    public enum RemoteIdentityType
    {
        Client = 0,
        Subscriber = 1,
        Server = 2
    }
    public class RequestMessage
    {
        public RequestMessage() { }
        public Guid ActivityId { get; set; }
        public Halibut.ServiceEndPoint Destination { get; set; }
        public string Id { get; set; }
        public string MethodName { get; set; }
        public Object[] Params { get; set; }
        public string ServiceName { get; set; }
        public string ToString() { }
    }
    public class ResponseMessage
    {
        public ResponseMessage() { }
        public Halibut.Transport.Protocol.ServerError Error { get; set; }
        public string Id { get; set; }
        public Object Result { get; set; }
        public static Halibut.Transport.Protocol.ResponseMessage FromError(Halibut.Transport.Protocol.RequestMessage request, string message) { }
        public static Halibut.Transport.Protocol.ResponseMessage FromException(Halibut.Transport.Protocol.RequestMessage request, Exception ex) { }
        public static Halibut.Transport.Protocol.ResponseMessage FromResult(Halibut.Transport.Protocol.RequestMessage request, Object result) { }
    }
    public class ServerError
    {
        public ServerError() { }
        public string Details { get; set; }
        public string Message { get; set; }
    }
    public class StreamCapture : IDisposable
    {
        public StreamCapture() { }
        public static Halibut.Transport.Protocol.StreamCapture Current { get; }
        public ICollection<Halibut.DataStream> DeserializedStreams { get; }
        public ICollection<Halibut.DataStream> SerializedStreams { get; }
        public void Dispose() { }
        public static Halibut.Transport.Protocol.StreamCapture New() { }
    }
    public class TemporaryFileDataStreamReceiver : Halibut.IDataStreamReceiver
    {
        public TemporaryFileDataStreamReceiver(Action<Stream> writer) { }
        public void Read(Action<Stream> reader) { }
        public void SaveTo(string filePath) { }
    }
    public class TemporaryFileStream : Halibut.IDataStreamReceiver
    {
        public TemporaryFileStream(string path, Halibut.Diagnostics.ILog log) { }
        protected void Finalize() { }
        public void Read(Action<Stream> reader) { }
        public void SaveTo(string filePath) { }
    }
    public class WebSocketStream : Stream, IDisposable
    {
        public WebSocketStream(WebSocket context) { }
        public bool CanRead { get; }
        public bool CanSeek { get; }
        public bool CanWrite { get; }
        public long Length { get; }
        public long Position { get; set; }
        protected void Dispose(bool disposing) { }
        public void Flush() { }
        public int Read(Byte[] buffer, int offset, int count) { }
        public Task<string> ReadTextMessage() { }
        public long Seek(long offset, SeekOrigin origin) { }
        public void SetLength(long value) { }
        public void Write(Byte[] buffer, int offset, int count) { }
        public Task WriteTextMessage(string message) { }
    }
}
namespace Halibut.Transport.Proxy
{
    public class HttpProxyClient : Halibut.Transport.Proxy.IProxyClient
    {
        public HttpProxyClient(Halibut.Diagnostics.ILog logger, string proxyHost, int proxyPort, string proxyUserName, string proxyPassword) { }
        public string ProxyHost { get; set; }
        public string ProxyName { get; }
        public string ProxyPassword { get; set; }
        public int ProxyPort { get; set; }
        public string ProxyUserName { get; set; }
        public TcpClient TcpClient { get; set; }
        public TcpClient CreateConnection(string destinationHost, int destinationPort, TimeSpan timeout) { }
        public Halibut.Transport.Proxy.IProxyClient WithTcpClientFactory(Func<TcpClient> tcpClientfactory) { }
    }
    public interface IProxyClient
    {
        public string ProxyHost { get; set; }
        public string ProxyName { get; }
        public int ProxyPort { get; set; }
        public TcpClient TcpClient { get; }
        public TcpClient CreateConnection(string destinationHost, int destinationPort, TimeSpan timeout) { }
        public Halibut.Transport.Proxy.IProxyClient WithTcpClientFactory(Func<TcpClient> tcpClientfactory) { }
    }
    public class ProxyClientFactory
    {
        public ProxyClientFactory() { }
        public Halibut.Transport.Proxy.IProxyClient CreateProxyClient(Halibut.Diagnostics.ILog logger, Halibut.Transport.Proxy.ProxyType type, string proxyHost, int proxyPort, string proxyUsername, string proxyPassword) { }
        public Halibut.Transport.Proxy.IProxyClient CreateProxyClient(Halibut.Diagnostics.ILog logger, Halibut.ProxyDetails proxyDetails) { }
    }
    public enum ProxyType
    {
        None = 0,
        HTTP = 1,
        SOCKS4 = 2,
        SOCKS4A = 3,
        SOCKS5 = 4
    }
}
namespace Halibut.Transport.Proxy.Exceptions
{
    public class ProxyException : Exception, ISerializable
    {
        public ProxyException() { }
        public ProxyException(string message) { }
        public ProxyException(string message, Exception innerException) { }
        protected ProxyException(SerializationInfo info, StreamingContext context) { }
    }
}