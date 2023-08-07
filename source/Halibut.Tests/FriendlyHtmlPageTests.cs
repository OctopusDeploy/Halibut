using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.ServiceModel;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.TestUtils.Contracts;
using NUnit.Framework;

namespace Halibut.Tests
{
    // These tests change the behaviour of ServicePointManager.ServerCertificateValidationCallback
    // And can interfere with other tests
    [NonParallelizable]
    public class FriendlyHtmlPageTests
    {
        static DelegateServiceFactory GetDelegateServiceFactory()
        {
            var services = new DelegateServiceFactory();
            services.Register<IEchoService>(() => new EchoService());
            return services;
        }

        [SyncAndAsyncTestCases("https://127.0.0.1:{port}")]
        [SyncAndAsyncTestCases("https://127.0.0.1:{port}/")]
        [SyncAndAsyncTestCases("https://localhost:{port}")]
        [SyncAndAsyncTestCases("https://localhost:{port}/")]
        [SyncAndAsyncTestCases("https://{machine}:{port}")]
        [SyncAndAsyncTestCases("https://{machine}:{port}/")]
        public async Task SupportsHttpsGet(SyncAndAsyncTestCase testCase)
        {
            using (var octopus = GetHalibutRuntime(GetDelegateServiceFactory(), testCase.SyncOrAsync))
            {
                var listenPort = octopus.Listen();
                var uri = testCase.Value.Replace("{machine}", Dns.GetHostName()).Replace("{port}", listenPort.ToString());

                var result = await DownloadStringIgnoringCertificateValidation(uri);

                result.Should().Be("<html><body><p>Hello!</p></body></html>");
            }
        }

        [FriendlyHtmlSyncAndAsyncTestCases("<html><body><h1>Welcome to Octopus Server!</h1><p>It looks like everything is running just like you expected, well done.</p></body></html>", null)]
        [FriendlyHtmlSyncAndAsyncTestCases("Simple text works too!", null)]
        [FriendlyHtmlSyncAndAsyncTestCases("", null)]
        [FriendlyHtmlSyncAndAsyncTestCases(null, "<html><body><p>Hello!</p></body></html>")]
        public async Task CanSetCustomFriendlyHtmlPage(FriendlyHtmlSyncAndAsyncTestCase testCase)
        {
            var expectedResult = testCase.Expected ?? testCase.Html; // Handle the null case which reverts to default html

            using (var octopus = GetHalibutRuntime(GetDelegateServiceFactory(), testCase.SyncOrAsync))
            {
                octopus.SetFriendlyHtmlPageContent(testCase.Html);
                var listenPort = octopus.Listen();

                var result = await DownloadStringIgnoringCertificateValidation("https://localhost:" + listenPort);

                result.Should().Be(expectedResult);
            }
        }

        [Test]
        [SyncAndAsync]
        public async Task CanSetCustomFriendlyHtmlPageHeaders(SyncOrAsync syncOrAsync)
        {
            using (var octopus = GetHalibutRuntime(GetDelegateServiceFactory(), syncOrAsync))
            {
                octopus.SetFriendlyHtmlPageHeaders(new Dictionary<string, string> { { "X-Content-Type-Options", "nosniff" }, { "X-Frame-Options", "DENY" } });
                var listenPort = octopus.Listen();

                var result = await GetHeadersIgnoringCertificateValidation("https://localhost:" + listenPort);

                result.Should().Contain(x => x.Key == "X-Content-Type-Options" && x.Value == "nosniff");
                result.Should().Contain(x => x.Key == "X-Frame-Options" && x.Value == "DENY");
            }
        }

        [Test]
        [SyncAndAsync]
        [System.ComponentModel.Description("Connecting over a non-secure connection should cause the socket to be closed by the server. The socket used to be held open indefinitely for any failure to establish an SslStream.")]
        public async Task ConnectingOverHttpShouldFailQuickly(SyncOrAsync syncOrAsync)
        {
            var logger = new SerilogLoggerBuilder().Build();
            using (var octopus = GetHalibutRuntime(GetDelegateServiceFactory(), syncOrAsync))
            {
                logger.Information("Halibut runtime created.");
                var listenPort = octopus.Listen();
                logger.Information("Got port to listen on..");
                var sw = new Stopwatch();
                await AssertAsync.Throws<HttpRequestException>(() =>
                {
                    logger.Information("Sending request.");
                    sw.Start();
                    try
                    {
                        return DownloadStringIgnoringCertificateValidation("http://localhost:" + listenPort);
                    }
                    finally
                    {
                        sw.Stop();
                    }
                });

                sw.Elapsed.Should().BeLessOrEqualTo(TimeSpan.FromSeconds(5));
            }
        }

        static async Task<string> DownloadStringIgnoringCertificateValidation(string uri)
        {
            using (var httpClientHandler = new HttpClientHandler())
            {
                httpClientHandler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
                using (var client = new HttpClient(httpClientHandler))
                {
                    return await client.GetStringAsync(uri);
                }
            }
        }

        static async Task<List<KeyValuePair<string, string>>> GetHeadersIgnoringCertificateValidation(string uri)
        {
            using (var httpClientHandler = new HttpClientHandler())
            {
                httpClientHandler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
                using (var client = new HttpClient(httpClientHandler))
                {
                    var headers = new List<KeyValuePair<string, string>>();
                    var existingServerCertificateValidationCallback = ServicePointManager.ServerCertificateValidationCallback;
                    try
                    {
                        // We need to ignore server certificate validation errors - the server certificate is self-signed
                        ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, errors) => true;
                        var response = await client.GetAsync(uri);
                        foreach (var key in response.Headers)
                        {
                            headers.Add(new KeyValuePair<string, string>(key.Key, key.Value.First()));
                        }
                    }
                    finally
                    {
                        // And restore it back to default behaviour
                        ServicePointManager.ServerCertificateValidationCallback = existingServerCertificateValidationCallback;
                    }

                    return headers;
                }
            }
        }

        HalibutRuntime GetHalibutRuntime(IServiceFactory serviceFactory, SyncOrAsync syncOrAsync)
        {
            var builder = new HalibutRuntimeBuilder().WithServiceFactory(serviceFactory).WithServerCertificate(Certificates.Octopus);
            if (syncOrAsync == SyncOrAsync.Async)
            {
                builder = builder.WithAsyncHalibutFeatureEnabled();
            }

            return builder.Build();
        }
    }
}
