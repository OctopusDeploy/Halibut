// Copyright 2012-2013 Octopus Deploy Pty. Ltd.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//   http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Exceptions;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices.Async;
using Halibut.Tests.Util;
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
    }
}