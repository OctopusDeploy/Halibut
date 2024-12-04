using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices.Async;
using Halibut.TestUtils.Contracts;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class TlsFixture : BaseTest
    {
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        public async Task LatestClientAndServiceUseBestAvailableSslProtocol(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                             .WithStandardServices()
                             .AsLatestClientAndLatestServiceBuilder()
                             .RecordingClientLogs(out var clientLogs)
                             .RecordingServiceLogs(out var serviceLogs)
                             .Build(CancellationToken))
            {
                Logger.Information("Platform detection:");
                Logger.Information("Environment.OSVersion.Platform: {EnvironmentOSVersionPlatform}", Environment.OSVersion.Platform);
                Logger.Information("Environment.OSVersion.Version: {EnvironmentOSVersionVersion}", Environment.OSVersion.Version);
                Logger.Information("Environment.OSVersion.VersionString: {EnvironmentOSVersionVersionString}", Environment.OSVersion.VersionString);
                Logger.Information("Environment.OSVersion.ServicePack: {EnvironmentOSVersionServicePack}", Environment.OSVersion.ServicePack);
                Logger.Information("RuntimeInformation.OSDescription: {RuntimeInformationOSDescription}", RuntimeInformation.OSDescription);
                Logger.Information("RuntimeInformation.RuntimeIdentifier: {RuntimeInformationRuntimeIdentifier}", RuntimeInformation.RuntimeIdentifier);
                Logger.Information("RuntimeInformation.FrameworkDescription: {RuntimeInformationFrameworkDescription}", RuntimeInformation.FrameworkDescription);
                Logger.Information("RuntimeInformation.ProcessArchitecture: {RuntimeInformationProcessArchitecture}", RuntimeInformation.ProcessArchitecture);
                Logger.Information("RuntimeInformation.OSArchitecture: {RuntimeInformationOSArchitecture}", RuntimeInformation.OSArchitecture);
                
                var echo = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>();
                await echo.SayHelloAsync("World");

                var connectionInitiatorLogs = clientAndServiceTestCase.ServiceConnectionType == ServiceConnectionType.Listening
                    ? clientLogs
                    : serviceLogs;

                // .NET does not support TLS 1.3 on Mac OS yet.
                // https://github.com/dotnet/runtime/issues/1979
                var expectedSslProtocol = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                    ? SslProtocols.Tls12
                    : SslProtocols.Tls13;

                connectionInitiatorLogs.Values
                    .SelectMany(log => log.GetLogs())
                    .Should().Contain(logEvent => logEvent.FormattedMessage.Contains($"using protocol {expectedSslProtocol}"));
            }
        }

        [Test]
        [LatestClientAndPreviousServiceVersionsTestCases(testNetworkConditions: false)]
        public async Task LatestClientAndPreviousServiceFallBackOnTls12(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                             .WithStandardServices()
                             .AsLatestClientAndPreviousServiceVersionBuilder()
                             .RecordingClientLogs(out var clientLogs)
                             .Build(CancellationToken))
            {
                var echo = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>();
                await echo.SayHelloAsync("World");

                var expectedLogMessage = clientAndServiceTestCase.ServiceConnectionType == ServiceConnectionType.Listening
                    ? $"using protocol {SslProtocols.Tls12}"
                    : $"client connected with {SslProtocols.Tls12}";

                clientLogs.Values
                    .SelectMany(log => log.GetLogs())
                    .Should().Contain(logEvent => logEvent.FormattedMessage.Contains(expectedLogMessage));
            }
        }
    }
}