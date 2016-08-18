using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Reflection;

namespace Halibut.Diagnostics
{
    public class HalibutLimits
    {
        static HalibutLimits()
        {
            // should be able to use Directory.GetCurrentDirectory()
            // to get the working path, but the nunit test runner
            // runs from another directory. Go with dll location for now.
#if NET40
            var directory = Path.GetDirectoryName(new Uri(typeof(HalibutLimits).Assembly.CodeBase).LocalPath);
#else
            var directory = Path.GetDirectoryName(new Uri(typeof(HalibutLimits).GetTypeInfo().Assembly.CodeBase).LocalPath);
#endif
            var builder = new ConfigurationBuilder();
            builder.SetBasePath(directory);
            builder.AddJsonFile("appsettings.json", optional: true);
            var halibutConfig = builder.Build();

            var fields = typeof (HalibutLimits).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            foreach (var field in fields)
            {
                var value = halibutConfig["Halibut:" + field.Name];
                if (string.IsNullOrWhiteSpace(value)) continue;
                var time = TimeSpan.Parse(value);
                field.SetValue(null, time);
            }
        }

        public static TimeSpan PollingRequestQueueTimeout = TimeSpan.FromMinutes(2);
        public static TimeSpan PollingRequestMaximumMessageProcessingTimeout = TimeSpan.FromMinutes(10);
        public static TimeSpan RetryListeningSleepInterval = TimeSpan.FromSeconds(1);
        public static TimeSpan ConnectionErrorRetryTimeout = TimeSpan.FromMinutes(5);
        public static TimeSpan TcpClientSendTimeout = TimeSpan.FromMinutes(10);
        public static TimeSpan TcpClientReceiveTimeout = TimeSpan.FromMinutes(10);
        public static TimeSpan TcpClientPooledConnectionTimeout = TimeSpan.FromMinutes(9);
        public static TimeSpan TcpClientHeartbeatSendTimeout = TimeSpan.FromSeconds(60);
        public static TimeSpan TcpClientHeartbeatReceiveTimeout = TimeSpan.FromSeconds(60);
        public static TimeSpan TcpClientConnectTimeout = TimeSpan.FromSeconds(60);
        public static TimeSpan PollingQueueWaitTimeout = TimeSpan.FromSeconds(30);
    }
}