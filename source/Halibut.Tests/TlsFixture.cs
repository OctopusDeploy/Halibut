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
        [LatestClientAndLatestServiceTestCases(testWebSocket: false, testNetworkConditions: false)]
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
                Logger.Information("RuntimeInformation.FrameworkDescription: {RuntimeInformationFrameworkDescription}", RuntimeInformation.FrameworkDescription);
                Logger.Information("RuntimeInformation.ProcessArchitecture: {RuntimeInformationProcessArchitecture}", RuntimeInformation.ProcessArchitecture);
                Logger.Information("RuntimeInformation.OSArchitecture: {RuntimeInformationOSArchitecture}", RuntimeInformation.OSArchitecture);
                
                var echo = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>();
                await echo.SayHelloAsync("World");

                var connectionInitiatorLogs = clientAndServiceTestCase.ServiceConnectionType == ServiceConnectionType.Listening
                    ? clientLogs
                    : serviceLogs;

                var expectedSslProtocol = GetExpectedSslProtocolForTheCurrentPlatform();
                var expectedLogFragment = $"using protocol {expectedSslProtocol}";
                
                connectionInitiatorLogs.Values
                    .SelectMany(log => log.GetLogs())
                    .Should().Contain(
                        logEvent => logEvent.FormattedMessage.Contains(expectedLogFragment),
                        "The OS is \"{{OSDescription}}\", so we expect {{expectedSslProtocol}} to be used, and expect log output to contain \"{expectedLogFragment}\" for {tentacleType} tentacles.",
                        RuntimeInformation.OSDescription,
                        expectedSslProtocol,
                        expectedLogFragment,
                        clientAndServiceTestCase.ServiceConnectionType);
            }
        }

        [Test]
        [LatestClientAndPreviousServiceVersionsTestCases(testWebSocket: false, testNetworkConditions: false)]
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

        SslProtocols GetExpectedSslProtocolForTheCurrentPlatform()
        {
            // All linux platforms we test against support TLS 1.3.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return SslProtocols.Tls13;
            }
            
            // We test against old versions of Windows which do not support TLS 1.3.
            // TLS 1.3 is supported since Windows Server 2022 which has build number 20348, and Windows 11 which has higher build numbers.
            // TLS 1.3 is partially supported in Windows 10, which can have lower build numbers, but we don't test against that so it is ignored here.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                const int WindowsServer2022OSBuild = 20348;
                return Environment.OSVersion.Version.Build >= WindowsServer2022OSBuild
                    ? SslProtocols.Tls13
                    : SslProtocols.Tls12;
            }

            // .NET does not support TLS 1.3 on Mac OS yet.
            // https://github.com/dotnet/runtime/issues/1979
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return SslProtocols.Tls12;
            }
            
            throw new NotSupportedException($"Unsupported OS platform: {RuntimeInformation.OSDescription}");
        }
    }
}