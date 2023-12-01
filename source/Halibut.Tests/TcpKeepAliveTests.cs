using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices.Async;
using Halibut.Tests.Util;
using Halibut.TestUtils.Contracts;
using Halibut.Transport;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class TcpKeepAliveTests : BaseTest
    {
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testListening:false, testWebSocket:false)]
        public async Task TcpKeepAliveShouldBeSetOnPollingSocketsByDefault(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            // Arrange
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                             .WithStandardServices()
                             .AsLatestClientAndLatestServiceBuilder()
                             .Build(CancellationToken))
            {
                var echoServiceClient = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>();

                // Act
                await echoServiceClient.SayHelloAsync("An initial RPC call");

                //Assert
                var pollingSocket = GetConnectionManagerActiveConnectionSocket(clientAndService.Service);
                pollingSocket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive).Should().Be(1);

                var listeningSocket = GetSecureListenerActiveClientSocket(clientAndService.Client);
                listeningSocket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive).Should().Be(1);
            }
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testListening: false, testWebSocket: false)]
        public async Task TcpKeepAliveShouldNotBeSetOnPollingSocketsIfNotEnabled(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            // Arrange
            var halibutTimeoutsAndLimits = new HalibutTimeoutsAndLimits
            {
                TcpKeepAliveEnabled = false
            };
            
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                             .WithStandardServices()
                             .AsLatestClientAndLatestServiceBuilder()
                             .WithHalibutTimeoutsAndLimits(halibutTimeoutsAndLimits)
                             .Build(CancellationToken))
            {
                var echoServiceClient = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>();

                // Act
                await echoServiceClient.SayHelloAsync("An initial RPC call");

                //Assert
                var pollingSocket = GetConnectionManagerActiveConnectionSocket(clientAndService.Service);
                pollingSocket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive).Should().Be(0);

                var listeningSocket = GetSecureListenerActiveClientSocket(clientAndService.Client);
                listeningSocket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive).Should().Be(0);
            }
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testListening: false, testWebSocket: false)]
        public async Task TcpKeepAliveShouldBeNotDisruptConnectionOnPollingSockets(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            // Arrange
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                             .WithStandardServices()
                             .AsLatestClientAndLatestServiceBuilder()
                             .Build(CancellationToken))
            {
                var echoServiceClient = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>();

                // Act
                await echoServiceClient.SayHelloAsync("An initial RPC call");
                await Task.Delay(20000, CancellationToken);

                // Assert
                await echoServiceClient.SayHelloAsync("Next call should just work");
            }
        }

        static Socket GetSecureListenerActiveClientSocket(HalibutRuntime halibutRuntime)
        {
            var listeners = halibutRuntime.ReflectionGetFieldValue<List<IDisposable>>("listeners");
            var secureListener = listeners.Should().ContainSingle().Subject;
            var tcpClientManager = secureListener.ReflectionGetFieldValue<TcpClientManager>("tcpClientManager");
            var activeClients = tcpClientManager.ReflectionGetFieldValue<Dictionary<string, HashSet<TcpClient>>>("activeClients");
            var activeClient = activeClients.Should().ContainSingle().Subject.Value;
            var tcpClient = activeClient.Should().ContainSingle().Subject;
            return tcpClient.Client;
        }

        static Socket GetConnectionManagerActiveConnectionSocket(HalibutRuntime halibutRuntime)
        {
            var connectionManager = halibutRuntime.ReflectionGetFieldValue<ConnectionManagerAsync>("connectionManager");
            var activeConnections = connectionManager.ReflectionGetFieldValue<Dictionary<ServiceEndPoint, HashSet<IConnection>>>("activeConnections");
            var activeConnection = activeConnections.Values.Should().ContainSingle().Subject;
            var disposableNotifierConnection = activeConnection.Should().ContainSingle().Subject;
            var secureConnection = disposableNotifierConnection.ReflectionGetFieldValue<Lazy<IConnection>>("connection").Value;
            var tcpClient = secureConnection.ReflectionGetFieldValue<TcpClient>("client");

            return tcpClient.Client;
        }
    }
}