# Timer-based Redis Pending Request Queue Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a second implementation of the Redis pending request queue that uses `System.Threading.Timer` instead of `Task.Delay`/`DelayWithoutException` for its internal waits, selectable via a static switch on `RedisPendingRequestQueueFactory`, with unit tests parametrized to run against both.

**Architecture:** Duplicate the three classes that own the queue's delay-based waits (`RedisPendingRequestQueue`, `NodeHeartBeatSender`, `NodeHeartBeatWatcher`) into `TimerBased*` siblings that call a new `TimerBasedDelay.Delay(...)` helper instead of `DelayWithoutException.Delay(...)`/`Task.Delay(...)`. A new `RedisPendingRequestQueueImplementation` enum + `RedisPendingRequestQueueFactory.Implementation` static switch picks which pair of classes `CreateQueue` builds. Both concrete classes implement a new internal `IRedisPendingRequestQueueTestControls` interface so tests (and the two existing test helpers that currently hard-cast to the concrete `RedisPendingRequestQueue` type) work against either implementation without caring which one is active.

**Tech Stack:** C# / .NET (net48;net8.0 multi-target — all new Redis-specific types are `#if NET8_0_OR_GREATER`), NUnit, FluentAssertions, NSubstitute.

## Global Constraints

- Spec: `docs/superpowers/specs/2026-08-11-timer-based-redis-queue-design.md`
- Out of scope (do not touch): `RedisPendingRequest`, `PollAndSubscribeToResponse`, `Cancellation/DelayBeforeSubscribingToRequestCancellation`, `Cancellation/WatchForRequestCancellation`, `NodeHeartBeat/HeartBeatInitialDelay`, `RedisHelpers/RedisFacade`, `RedisDataLossDetection/WatchForRedisLosingAllItsData`, `Halibut.Tests.Support.TestAttributes.PollingQueueTestCase`, `ClientAndServiceTestCasesBuilder`.
- Every new Redis-specific production file is wrapped in `#if NET8_0_OR_GREATER ... #endif`, matching every existing file under `Halibut/Queue/Redis/`.
- `RedisPendingRequestQueueFactory.Implementation` defaults to `RedisPendingRequestQueueImplementation.TaskDelay` — production behavior must be unchanged unless something explicitly sets it to `Timer`.
- `Halibut.Tests` has `InternalsVisibleTo` access to `Halibut` (see `source/Halibut/Properties/AssemblyInfo.cs:19`), so `internal` types/members are usable from tests without making them `public`.
- Do not change accessibility (internal → public) of any existing member on `RedisPendingRequestQueue` — use explicit interface implementation for members that must stay `internal`.

---

### Task 1: Extract `WatcherAndDisposables` out of `RedisPendingRequestQueue`

`RedisPendingRequestQueue` currently declares `WatcherAndDisposables` as a nested class. Both `RedisPendingRequestQueue` and the new `TimerBasedRedisPendingRequestQueue` (Task 6) need this exact type (it's not part of the Timer-vs-TaskDelay comparison — it's a plain holder used identically by both), so it must become a shared top-level type before Task 6 can reference it.

**Files:**
- Create: `source/Halibut/Queue/Redis/WatcherAndDisposables.cs`
- Modify: `source/Halibut/Queue/Redis/RedisPendingRequestQueue.cs`

**Interfaces:**
- Produces: `Halibut.Queue.Redis.WatcherAndDisposables` (internal class, implements `IAsyncDisposable`), constructor `WatcherAndDisposables(DisposableCollection, CancellationToken, WatchForRequestCancellationOrSenderDisconnect)`, property `RequestCancelledForAnyReasonCancellationToken` (`CancellationToken`), property `Watcher` (`WatchForRequestCancellationOrSenderDisconnect`).

- [ ] **Step 1: Create the standalone `WatcherAndDisposables` class**

Create `source/Halibut/Queue/Redis/WatcherAndDisposables.cs`:

```csharp

#if NET8_0_OR_GREATER
using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Queue.Redis.Cancellation;
using Halibut.Util;

namespace Halibut.Queue.Redis
{
    internal class WatcherAndDisposables : IAsyncDisposable
    {
        readonly DisposableCollection disposableCollection;
        public CancellationToken RequestCancelledForAnyReasonCancellationToken { get; }
        public WatchForRequestCancellationOrSenderDisconnect Watcher { get; }

        public WatcherAndDisposables(DisposableCollection disposableCollection, CancellationToken requestCancelledForAnyReasonCancellationToken, WatchForRequestCancellationOrSenderDisconnect watcher)
        {
            this.disposableCollection = disposableCollection;
            this.RequestCancelledForAnyReasonCancellationToken = requestCancelledForAnyReasonCancellationToken;
            this.Watcher = watcher;
        }

        public async ValueTask DisposeAsync()
        {
            await Try.IgnoringError(async () => await disposableCollection.DisposeAsync());
        }
    }
}
#endif
```

- [ ] **Step 2: Remove the nested class from `RedisPendingRequestQueue.cs`**

In `source/Halibut/Queue/Redis/RedisPendingRequestQueue.cs`, find:

```csharp
        public class WatcherAndDisposables : IAsyncDisposable
        {
            readonly DisposableCollection disposableCollection;
            public CancellationToken RequestCancelledForAnyReasonCancellationToken { get; }
            public WatchForRequestCancellationOrSenderDisconnect Watcher { get; }

            public WatcherAndDisposables(DisposableCollection disposableCollection, CancellationToken requestCancelledForAnyReasonCancellationToken, WatchForRequestCancellationOrSenderDisconnect watcher)
            {
                this.disposableCollection = disposableCollection;
                this.RequestCancelledForAnyReasonCancellationToken = requestCancelledForAnyReasonCancellationToken;
                this.Watcher = watcher;
            }

            public async ValueTask DisposeAsync()
            {
                await Try.IgnoringError(async () => await disposableCollection.DisposeAsync());
            }
        }

        public const string RequestAbandonedMessage = "The request was abandoned, possibly because the node processing the request shutdown or redis lost all of its data.";
```

Replace with just:

```csharp
        public const string RequestAbandonedMessage = "The request was abandoned, possibly because the node processing the request shutdown or redis lost all of its data.";
```

(`RedisPendingRequestQueue` already has `using Halibut.Queue.Redis.Cancellation;` and `using Halibut.Util;` at the top, so no using-statement changes are needed — the type is now resolved from the new top-level `Halibut.Queue.Redis.WatcherAndDisposables` file in the same namespace.)

- [ ] **Step 3: Build and confirm no other references break**

Run: `dotnet build source/Halibut/Halibut.csproj -f net8.0`
Expected: Build succeeds. (`RedisPendingRequestQueue.DisposablesForInFlightRequests`, `ApplyResponse`, and `DequeueAsync` all reference `WatcherAndDisposables` by simple name — since it's now a top-level type in the same namespace, no call site needs to change.)

- [ ] **Step 4: Commit**

```bash
git add source/Halibut/Queue/Redis/WatcherAndDisposables.cs source/Halibut/Queue/Redis/RedisPendingRequestQueue.cs
git commit -m "Extract WatcherAndDisposables out of RedisPendingRequestQueue into a shared top-level type"
```

---

### Task 2: Add `IRedisPendingRequestQueueTestControls` and implement it on `RedisPendingRequestQueue`

Tests (Task 8, 9, 10) and two existing test helpers need to hold a variable that could be either `RedisPendingRequestQueue` or `TimerBasedRedisPendingRequestQueue` (Task 6) and still call queue-specific members (`WaitUntilQueueIsSubscribedToReceiveMessages`, the heartbeat timeout/rate properties, etc.) that aren't part of `IPendingRequestQueue`. This task defines that shared surface and wires it onto the existing class without changing any existing member's accessibility.

**Files:**
- Create: `source/Halibut/Queue/Redis/IRedisPendingRequestQueueTestControls.cs`
- Modify: `source/Halibut/Queue/Redis/RedisPendingRequestQueue.cs`

**Interfaces:**
- Consumes: `Halibut.Queue.Redis.WatcherAndDisposables` (Task 1).
- Produces: `Halibut.Queue.Redis.IRedisPendingRequestQueueTestControls : IPendingRequestQueue` — internal interface with members `RequestSenderNodeHeartBeatTimeout`, `RequestSenderNodeHeartBeatRate`, `RequestReceiverNodeHeartBeatTimeout`, `RequestReceiverNodeHeartBeatRate` (all `TimeSpan`, get/set), `TimeBetweenCheckingIfRequestWasCollected` (`TimeSpan`, get/set), `HeartBeatInitialDelay` (`HeartBeatInitialDelay`, get/set), `DelayBeforeSubscribingToRequestCancellation` (`DelayBeforeSubscribingToRequestCancellation`, get/set), `DisposablesForInFlightRequests` (`ConcurrentDictionary<Guid, WatcherAndDisposables>`, get-only), `WaitUntilQueueIsSubscribedToReceiveMessages()` (`Task`).

- [ ] **Step 1: Create the interface**

Create `source/Halibut/Queue/Redis/IRedisPendingRequestQueueTestControls.cs`:

```csharp

#if NET8_0_OR_GREATER
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Halibut.Queue.Redis.Cancellation;
using Halibut.Queue.Redis.NodeHeartBeat;
using Halibut.ServiceModel;

namespace Halibut.Queue.Redis
{
    /// <summary>
    /// The members of RedisPendingRequestQueue / TimerBasedRedisPendingRequestQueue that test
    /// code needs to reach regardless of which concrete implementation is active behind
    /// RedisPendingRequestQueueFactory.Implementation.
    /// </summary>
    internal interface IRedisPendingRequestQueueTestControls : IPendingRequestQueue
    {
        TimeSpan RequestSenderNodeHeartBeatTimeout { get; set; }
        TimeSpan RequestSenderNodeHeartBeatRate { get; set; }
        TimeSpan RequestReceiverNodeHeartBeatTimeout { get; set; }
        TimeSpan RequestReceiverNodeHeartBeatRate { get; set; }
        TimeSpan TimeBetweenCheckingIfRequestWasCollected { get; set; }
        HeartBeatInitialDelay HeartBeatInitialDelay { get; set; }
        DelayBeforeSubscribingToRequestCancellation DelayBeforeSubscribingToRequestCancellation { get; set; }
        ConcurrentDictionary<Guid, WatcherAndDisposables> DisposablesForInFlightRequests { get; }
        Task WaitUntilQueueIsSubscribedToReceiveMessages();
    }
}
#endif
```

- [ ] **Step 2: Implement it on `RedisPendingRequestQueue`**

In `source/Halibut/Queue/Redis/RedisPendingRequestQueue.cs`, change the class declaration from:

```csharp
    class RedisPendingRequestQueue : IPendingRequestQueue, IDisposable
```

to:

```csharp
    class RedisPendingRequestQueue : IRedisPendingRequestQueueTestControls, IDisposable
```

`RequestReceiverNodeHeartBeatTimeout`, `HeartBeatInitialDelay`, and `DelayBeforeSubscribingToRequestCancellation` are already `public` with matching signatures, so they implicitly satisfy the interface — no change needed to those three properties.

The remaining interface members (`RequestSenderNodeHeartBeatTimeout`, `RequestSenderNodeHeartBeatRate`, `RequestReceiverNodeHeartBeatRate`, `TimeBetweenCheckingIfRequestWasCollected`, `DisposablesForInFlightRequests`, `WaitUntilQueueIsSubscribedToReceiveMessages()`) are `internal` and must stay that way, so add explicit interface implementations. Find:

```csharp
        public DelayBeforeSubscribingToRequestCancellation DelayBeforeSubscribingToRequestCancellation { get; set; } = DefaultDelayBeforeSubscribingToRequestCancellation;
        
        public RedisPendingRequestQueue(
```

Replace with:

```csharp
        public DelayBeforeSubscribingToRequestCancellation DelayBeforeSubscribingToRequestCancellation { get; set; } = DefaultDelayBeforeSubscribingToRequestCancellation;

        TimeSpan IRedisPendingRequestQueueTestControls.RequestSenderNodeHeartBeatTimeout
        {
            get => RequestSenderNodeHeartBeatTimeout;
            set => RequestSenderNodeHeartBeatTimeout = value;
        }

        TimeSpan IRedisPendingRequestQueueTestControls.RequestSenderNodeHeartBeatRate
        {
            get => RequestSenderNodeHeartBeatRate;
            set => RequestSenderNodeHeartBeatRate = value;
        }

        TimeSpan IRedisPendingRequestQueueTestControls.RequestReceiverNodeHeartBeatRate
        {
            get => RequestReceiverNodeHeartBeatRate;
            set => RequestReceiverNodeHeartBeatRate = value;
        }

        TimeSpan IRedisPendingRequestQueueTestControls.TimeBetweenCheckingIfRequestWasCollected
        {
            get => TimeBetweenCheckingIfRequestWasCollected;
            set => TimeBetweenCheckingIfRequestWasCollected = value;
        }

        ConcurrentDictionary<Guid, WatcherAndDisposables> IRedisPendingRequestQueueTestControls.DisposablesForInFlightRequests => DisposablesForInFlightRequests;

        Task IRedisPendingRequestQueueTestControls.WaitUntilQueueIsSubscribedToReceiveMessages() => WaitUntilQueueIsSubscribedToReceiveMessages();
        
        public RedisPendingRequestQueue(
```

- [ ] **Step 3: Build**

Run: `dotnet build source/Halibut/Halibut.csproj -f net8.0`
Expected: Build succeeds.

- [ ] **Step 4: Run the existing Redis queue fixture to confirm no behavior change**

Run: `dotnet test source/Halibut.Tests/Halibut.Tests.csproj -f net8.0 --filter "FullyQualifiedName~RedisPendingRequestQueueFixture"`
Expected: All tests pass (requires a local Redis instance — same precondition these tests already have via `[RedisTest]`). This is a pure interface-implementation change with no logic change, so this is a regression check rather than a new test.

- [ ] **Step 5: Commit**

```bash
git add source/Halibut/Queue/Redis/IRedisPendingRequestQueueTestControls.cs source/Halibut/Queue/Redis/RedisPendingRequestQueue.cs
git commit -m "Add IRedisPendingRequestQueueTestControls and implement it on RedisPendingRequestQueue"
```

---

### Task 3: Add `TimerBasedDelay`

**Files:**
- Create: `source/Halibut/Util/TimerBasedDelay.cs`
- Test: `source/Halibut.Tests/Util/TimerBasedDelayFixture.cs`

**Interfaces:**
- Produces: `Halibut.Util.TimerBasedDelay.Delay(TimeSpan timeSpan, CancellationToken cancellationToken) -> Task` — drop-in replacement for `Halibut.Util.DelayWithoutException.Delay`, same non-throwing-on-cancellation contract.

- [ ] **Step 1: Write the failing test**

Create `source/Halibut.Tests/Util/TimerBasedDelayFixture.cs`:

```csharp
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Util;
using NUnit.Framework;

namespace Halibut.Tests.Util
{
    public class TimerBasedDelayFixture : BaseTest
    {
        [Test]
        public async Task Delay_ShouldNotThrow_WhenCancelledBeforeStarting()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            await TimerBasedDelay.Delay(TimeSpan.FromDays(1), cts.Token);
        }

        [Test]
        public async Task Delay_ShouldNotThrow_WhenCancelledPartway()
        {
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(10));
            await TimerBasedDelay.Delay(TimeSpan.FromDays(1), cts.Token);
        }

        [Test]
        public async Task Delay_ShouldCompleteAfterAtLeastTheGivenTimeSpan_WhenNotCancelled()
        {
            var stopwatch = Stopwatch.StartNew();
            await TimerBasedDelay.Delay(TimeSpan.FromMilliseconds(200), CancellationToken.None);
            stopwatch.Stop();

            stopwatch.Elapsed.Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(150));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test source/Halibut.Tests/Halibut.Tests.csproj -f net8.0 --filter "FullyQualifiedName~TimerBasedDelayFixture"`
Expected: FAIL to build with `error CS0234: The type or namespace name 'TimerBasedDelay' does not exist in the namespace 'Halibut.Util'`.

- [ ] **Step 3: Implement `TimerBasedDelay`**

Create `source/Halibut/Util/TimerBasedDelay.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Util
{
    /// <summary>
    /// Drop-in replacement for DelayWithoutException.Delay backed by a one-shot System.Threading.Timer
    /// and a TaskCompletionSource, instead of Task.Delay(...).ContinueWith(...). Never throws on
    /// cancellation - the returned task simply completes early.
    /// </summary>
    public static class TimerBasedDelay
    {
        public static Task Delay(TimeSpan timeSpan, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            Timer? timer = null;
            CancellationTokenRegistration registration = default;

            void Complete()
            {
                if (tcs.TrySetResult(true))
                {
                    registration.Dispose();
                    timer?.Dispose();
                }
            }

            timer = new Timer(_ => Complete(), null, timeSpan, Timeout.InfiniteTimeSpan);
            registration = cancellationToken.Register(Complete);

            return tcs.Task;
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test source/Halibut.Tests/Halibut.Tests.csproj -f net8.0 --filter "FullyQualifiedName~TimerBasedDelayFixture"`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add source/Halibut/Util/TimerBasedDelay.cs source/Halibut.Tests/Util/TimerBasedDelayFixture.cs
git commit -m "Add TimerBasedDelay, a Timer-backed drop-in replacement for DelayWithoutException"
```

---

### Task 4: Add `TimerBasedNodeHeartBeatSender`

Literal copy of `NodeHeartBeat/NodeHeartBeatSender.cs` with the class renamed and its one `DelayWithoutException.Delay` call replaced with `TimerBasedDelay.Delay`.

**Files:**
- Create: `source/Halibut/Queue/Redis/NodeHeartBeat/TimerBasedNodeHeartBeatSender.cs`
- Test: `source/Halibut.Tests/Queue/Redis/NodeHeartBeat/TimerBasedNodeHeartBeatSenderFixture.cs`

**Interfaces:**
- Consumes: `Halibut.Util.TimerBasedDelay.Delay` (Task 3).
- Produces: `Halibut.Queue.Redis.NodeHeartBeat.TimerBasedNodeHeartBeatSender : IAsyncDisposable`, constructor identical in shape to `NodeHeartBeatSender(Uri, Guid, IHalibutRedisTransport, ILog, HalibutQueueNodeSendingPulses, Func<HeartBeatMessage>, TimeSpan, HeartBeatInitialDelay)`.

- [ ] **Step 1: Write the failing test**

Create `source/Halibut.Tests/Queue/Redis/NodeHeartBeat/TimerBasedNodeHeartBeatSenderFixture.cs`:

```csharp

#if NET8_0_OR_GREATER
using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Queue.Redis.NodeHeartBeat;
using Halibut.Queue.Redis.RedisHelpers;
using Halibut.Tests.Queue.Redis.Utils;
using Halibut.Tests.Support.Logging;
using Halibut.Tests.TestSetup.Redis;
using Halibut.Logging;
using NUnit.Framework;

namespace Halibut.Tests.Queue.Redis.NodeHeartBeat
{
    [RedisTest]
    public class TimerBasedNodeHeartBeatSenderFixture : BaseTest
    {
        [Test]
        public async Task SendsAtLeastOneHeartBeat()
        {
            var endpoint = new Uri("poll://" + Guid.NewGuid());
            var requestActivityId = Guid.NewGuid();
            await using var redisFacade = RedisFacadeBuilder.CreateRedisFacade();
            var redisTransport = new HalibutRedisTransport(redisFacade);
            var log = new TestContextLogCreator("HeartBeat", LogLevel.Trace).CreateNewForPrefix("");

            await using var sender = new TimerBasedNodeHeartBeatSender(
                endpoint,
                requestActivityId,
                redisTransport,
                log,
                HalibutQueueNodeSendingPulses.RequestSenderNode,
                () => new global::Halibut.Queue.Redis.NodeHeartBeat.HeartBeatMessage(),
                TimeSpan.FromMilliseconds(50),
                new global::Halibut.Queue.Redis.NodeHeartBeat.HeartBeatInitialDelay(TimeSpan.Zero));

            var sawHeartBeat = false;
            await using var subscriptionCts = new global::Halibut.Util.CancelOnDisposeCancellationToken();
            var subscription = Task.Run(async () => await redisTransport.SubscribeToNodeHeartBeatChannel(
                endpoint,
                requestActivityId,
                HalibutQueueNodeSendingPulses.RequestSenderNode,
                async _ =>
                {
                    sawHeartBeat = true;
                    await Task.CompletedTask;
                },
                subscriptionCts.Token));

            await Task.Delay(TimeSpan.FromSeconds(2));
            await subscriptionCts.CancelAsync();
            await (await subscription).DisposeAsync();

            sawHeartBeat.Should().BeTrue();
        }
    }
}
#endif
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test source/Halibut.Tests/Halibut.Tests.csproj -f net8.0 --filter "FullyQualifiedName~TimerBasedNodeHeartBeatSenderFixture"`
Expected: FAIL to build with `error CS0246: The type or namespace name 'TimerBasedNodeHeartBeatSender' could not be found`.

- [ ] **Step 3: Implement `TimerBasedNodeHeartBeatSender`**

Create `source/Halibut/Queue/Redis/NodeHeartBeat/TimerBasedNodeHeartBeatSender.cs`:

```csharp

#if NET8_0_OR_GREATER
using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Queue.QueuedDataStreams;
using Halibut.Queue.Redis.RedisHelpers;
using Halibut.Util;

namespace Halibut.Queue.Redis.NodeHeartBeat
{
    public class TimerBasedNodeHeartBeatSender : IAsyncDisposable
    {
        readonly Uri endpoint;
        readonly Guid requestActivityId;
        readonly IHalibutRedisTransport halibutRedisTransport;
        readonly CancelOnDisposeCancellationToken cts;
        readonly ILog log;
        readonly HalibutQueueNodeSendingPulses nodeSendingPulsesType;

        internal Task TaskSendingPulses;
        public TimerBasedNodeHeartBeatSender(
            Uri endpoint,
            Guid requestActivityId,
            IHalibutRedisTransport halibutRedisTransport,
            ILog log,
            HalibutQueueNodeSendingPulses nodeSendingPulsesType,
            Func<HeartBeatMessage> heartBeatMessageProvider,
            TimeSpan defaultDelayBetweenPulses,
            HeartBeatInitialDelay heartBeatInitialDelay)
        {
            this.endpoint = endpoint;
            this.requestActivityId = requestActivityId;
            this.halibutRedisTransport = halibutRedisTransport;
            this.nodeSendingPulsesType = nodeSendingPulsesType;
            cts = new CancelOnDisposeCancellationToken();
            this.log = log.ForContext<TimerBasedNodeHeartBeatSender>();
            this.log.Write(EventType.Diagnostic, "Starting TimerBasedNodeHeartBeatSender for {0} node, request {1}, endpoint {2}", nodeSendingPulsesType, requestActivityId, endpoint);
            TaskSendingPulses = Task.Run(() => SendPulsesWhileProcessingRequest(heartBeatMessageProvider, defaultDelayBetweenPulses, heartBeatInitialDelay, cts.Token));
        }

        async Task SendPulsesWhileProcessingRequest(
            Func<HeartBeatMessage> heartBeatMessageProvider, 
            TimeSpan defaultDelayBetweenPulses, 
            HeartBeatInitialDelay heartBeatInitialDelay,
            CancellationToken cancellationToken)
        {
            await heartBeatInitialDelay.WaitBeforeHeartBeatSendingOrReceiving(cancellationToken);
            if(cancellationToken.IsCancellationRequested) return;
            
            log.Write(EventType.Diagnostic, "Starting heartbeat pulse loop for {0} node, request {1}", nodeSendingPulsesType, requestActivityId);
            
            TimeSpan delayBetweenPulse;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var heartBeatMessage = heartBeatMessageProvider();
                    var nodeHeartBeatMessage = HeartBeatMessage.Serialize(heartBeatMessage);
                    await halibutRedisTransport.SendNodeHeartBeat(endpoint, requestActivityId, nodeSendingPulsesType, nodeHeartBeatMessage, cancellationToken);
                    delayBetweenPulse = defaultDelayBetweenPulses;
                    log.Write(EventType.Diagnostic, "Successfully sent heartbeat for {0} node, request {1}, next pulse in {2} seconds", nodeSendingPulsesType, requestActivityId, delayBetweenPulse.TotalSeconds);
                }
                catch (Exception ex)
                {
                    if(cancellationToken.IsCancellationRequested) 
                    {
                        log.Write(EventType.Diagnostic, "Heartbeat pulse loop cancelled for {0} node, request {1}", nodeSendingPulsesType, requestActivityId);
                        return;
                    }
                    // Send pulses more frequently when we were unable to send a pulse.
                    delayBetweenPulse = defaultDelayBetweenPulses / 2;
                    log.WriteException(EventType.Diagnostic, "Failed to send heartbeat for {0} node, request {1}, switching to panic mode with {2} second intervals", ex, nodeSendingPulsesType, requestActivityId, delayBetweenPulse.TotalSeconds);
                }
                
                await TimerBasedDelay.Delay(delayBetweenPulse, cancellationToken);
            }
            
            log.Write(EventType.Diagnostic, "Heartbeat pulse loop ended for {0} node, request {1}", nodeSendingPulsesType, requestActivityId);
        }

        public async ValueTask DisposeAsync()
        {
            log.Write(EventType.Diagnostic, "Disposing TimerBasedNodeHeartBeatSender for {0} node, request {1}", nodeSendingPulsesType, requestActivityId);
            
            await Try.IgnoringError(async () => await cts.DisposeAsync());
            
            log.Write(EventType.Diagnostic, "TimerBasedNodeHeartBeatSender disposed for {0} node, request {1}", nodeSendingPulsesType, requestActivityId);
        }
    }
}
#endif
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test source/Halibut.Tests/Halibut.Tests.csproj -f net8.0 --filter "FullyQualifiedName~TimerBasedNodeHeartBeatSenderFixture"`
Expected: PASS (requires local Redis, same as other `[RedisTest]`-gated tests).

- [ ] **Step 5: Commit**

```bash
git add source/Halibut/Queue/Redis/NodeHeartBeat/TimerBasedNodeHeartBeatSender.cs source/Halibut.Tests/Queue/Redis/NodeHeartBeat/TimerBasedNodeHeartBeatSenderFixture.cs
git commit -m "Add TimerBasedNodeHeartBeatSender, a Timer-based copy of NodeHeartBeatSender"
```

---

### Task 5: Add `TimerBasedNodeHeartBeatWatcher`

Literal copy of `NodeHeartBeat/NodeHeartBeatWatcher.cs` with the class renamed and both `DelayWithoutException.Delay` calls replaced with `TimerBasedDelay.Delay`.

**Files:**
- Create: `source/Halibut/Queue/Redis/NodeHeartBeat/TimerBasedNodeHeartBeatWatcher.cs`

**Interfaces:**
- Consumes: `Halibut.Util.TimerBasedDelay.Delay` (Task 3).
- Produces: `Halibut.Queue.Redis.NodeHeartBeat.TimerBasedNodeHeartBeatWatcher` (static class) with static methods `WatchThatNodeProcessingTheRequestIsStillAlive(Uri, RequestMessage, RedisPendingRequest, IHalibutRedisTransport, TimeSpan, ILog, TimeSpan, IGetNotifiedOfHeartBeats, CancellationToken) -> Task<NodeWatcherResult>` and `WatchThatNodeWhichSentTheRequestIsStillAlive(Uri, Guid, IHalibutRedisTransport, ILog, HeartBeatInitialDelay, TimeSpan, CancellationToken) -> Task<NodeWatcherResult>` — same signatures as `NodeHeartBeatWatcher`.

No dedicated unit test for this task: it has no independently reachable seam without a full queue around it (it takes a live `RedisPendingRequest`/transport). Its behavior is exercised end-to-end by Task 6's smoke test and, comprehensively, by Task 10's parametrized `RedisPendingRequestQueueFixture` run.

- [ ] **Step 1: Implement `TimerBasedNodeHeartBeatWatcher`**

Create `source/Halibut/Queue/Redis/NodeHeartBeat/TimerBasedNodeHeartBeatWatcher.cs`:

```csharp
#if NET8_0_OR_GREATER
using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Queue.QueuedDataStreams;
using Halibut.Queue.Redis.RedisHelpers;
using Halibut.Transport.Protocol;
using Halibut.Util;

namespace Halibut.Queue.Redis.NodeHeartBeat
{
    public class TimerBasedNodeHeartBeatWatcher
    {
        public static async Task<NodeWatcherResult> WatchThatNodeProcessingTheRequestIsStillAlive(Uri endpoint,
            RequestMessage request,
            RedisPendingRequest redisPending,
            IHalibutRedisTransport halibutRedisTransport,
            TimeSpan timeBetweenCheckingIfRequestWasCollected,
            ILog log,
            TimeSpan maxTimeBetweenHeartBeetsBeforeProcessingNodeIsAssumedToBeOffline,
            IGetNotifiedOfHeartBeats notifiedOfHeartBeats,
            CancellationToken watchCancellationToken)
        {
            log = log.ForContext<TimerBasedNodeHeartBeatWatcher>();
            // Once the pending's CT has been cancelled we no longer care to keep observing
            await using var cts = new CancelOnDisposeCancellationToken(watchCancellationToken, redisPending.PendingRequestCancellationToken);
            try
            {
                await WaitForRequestToBeCollected(endpoint, request, redisPending, halibutRedisTransport, timeBetweenCheckingIfRequestWasCollected, log, cts.Token);
                if (redisPending.HasResponseBeenSet()) return NodeWatcherResult.NoDisconnectSeen;

                return await WatchForPulsesFromNode(
                    endpoint, 
                    request.ActivityId,
                    halibutRedisTransport,
                    log,
                    maxTimeBetweenHeartBeetsBeforeProcessingNodeIsAssumedToBeOffline,
                    HalibutQueueNodeSendingPulses.RequestProcessorNode,
                    cts.Token,
                    async heartBeatMessage => await notifiedOfHeartBeats.HeartBeatReceived(heartBeatMessage, redisPending.PendingRequestCancellationToken));
            }
            catch (Exception) when (cts.Token.IsCancellationRequested)
            {
                return NodeWatcherResult.NoDisconnectSeen;
            }
            catch (Exception)
            {
                return NodeWatcherResult.NodeMayHaveDisconnected;
            }
        }

        public static async Task<NodeWatcherResult> WatchThatNodeWhichSentTheRequestIsStillAlive(
            Uri endpoint,
            Guid requestActivityId,
            IHalibutRedisTransport halibutRedisTransport,
            ILog log,
            HeartBeatInitialDelay heartBeatInitialDelay,
            TimeSpan maxTimeBetweenSenderHeartBeetsBeforeSenderIsAssumedToBeOffline,
            CancellationToken watchCancellationToken)
        {
            await heartBeatInitialDelay.WaitBeforeHeartBeatSendingOrReceiving(watchCancellationToken);
            if (watchCancellationToken.IsCancellationRequested)
            {
                return NodeWatcherResult.NoDisconnectSeen;
            }
            
            try
            {
                return await WatchForPulsesFromNode(endpoint, requestActivityId, halibutRedisTransport, log, maxTimeBetweenSenderHeartBeetsBeforeSenderIsAssumedToBeOffline, HalibutQueueNodeSendingPulses.RequestSenderNode, watchCancellationToken);
            }
            catch (Exception) when (watchCancellationToken.IsCancellationRequested)
            {
                return NodeWatcherResult.NoDisconnectSeen;
            }
            catch (Exception)
            {
                return NodeWatcherResult.NodeMayHaveDisconnected;
            }
        }

        static async Task<NodeWatcherResult> WatchForPulsesFromNode(Uri endpoint,
            Guid requestActivityId,
            IHalibutRedisTransport halibutRedisTransport,
            ILog log,
            TimeSpan maxTimeBetweenHeartBeetsBeforeNodeIsAssumedToBeOffline,
            HalibutQueueNodeSendingPulses watchingForPulsesFrom,
            CancellationToken watchCancellationToken,
            Func<HeartBeatMessage, Task>? notifiedOfHeartBeats = null)
        {
            
            log.ForContext<TimerBasedNodeHeartBeatSender>();
            log.Write(EventType.Diagnostic, "Starting to watch for pulses from {0} node, request {1}, endpoint {2}", watchingForPulsesFrom, requestActivityId, endpoint);

            DateTimeOffset? lastHeartBeat = DateTimeOffset.Now;

            await using var subscriptionCts = new CancelOnDisposeCancellationToken(watchCancellationToken);
            // Non-blocking subscription for heart beats. 
            var subscriptionTask = Task.Run(async () => await halibutRedisTransport.SubscribeToNodeHeartBeatChannel(
                endpoint,
                requestActivityId,
                watchingForPulsesFrom,
                async heartBeatMessageJson =>
                {
                    await Task.CompletedTask;
                    lastHeartBeat = DateTimeOffset.Now;
                    log.Write(EventType.Diagnostic, "Received heartbeat from {0} node, request {1}", watchingForPulsesFrom, requestActivityId);
                    if (notifiedOfHeartBeats != null)
                    {
                        try
                        {
                            var heartBeatMessage = HeartBeatMessage.Deserialize(heartBeatMessageJson);
                            await notifiedOfHeartBeats(heartBeatMessage);
                        }
                        catch (Exception ex)
                        {
                            log.WriteException(EventType.Diagnostic, "Failed to deserialize heartbeat message from {0} node, request {1}. JSON: {2}", ex, watchingForPulsesFrom, requestActivityId, heartBeatMessageJson);
                        }
                    }
                }, subscriptionCts.Token));
            try
            {
                try
                {

                    while (!watchCancellationToken.IsCancellationRequested)
                    {
                        var timeSinceLastHeartBeat = DateTimeOffset.Now - lastHeartBeat.Value;
                        if (timeSinceLastHeartBeat > maxTimeBetweenHeartBeetsBeforeNodeIsAssumedToBeOffline)
                        {
                            log.Write(EventType.Diagnostic, "{0} node appears disconnected, request {1}, last heartbeat was {2} seconds ago", watchingForPulsesFrom, requestActivityId, timeSinceLastHeartBeat.TotalSeconds);
                            return NodeWatcherResult.NodeMayHaveDisconnected;
                        }

                        var timeToWait = TimeSpanHelper.Min(
                            TimeSpan.FromSeconds(30),
                            maxTimeBetweenHeartBeetsBeforeNodeIsAssumedToBeOffline - timeSinceLastHeartBeat + TimeSpan.FromSeconds(1));

                        await TimerBasedDelay.Delay(timeToWait, watchCancellationToken);
                    }

                    log.Write(EventType.Diagnostic, "{0} node watcher cancelled, request {1}", watchingForPulsesFrom, requestActivityId);
                    return NodeWatcherResult.NoDisconnectSeen;
                }
                catch (Exception ex) when (!watchCancellationToken.IsCancellationRequested)
                {
                    log.WriteException(EventType.Diagnostic, "Error while watching {0} node, request {1}", ex, watchingForPulsesFrom, requestActivityId);
                    throw;
                }
            }
            finally
            {
                await Try.IgnoringError(async () => await subscriptionCts.CancelAsync());
                await Try.IgnoringError(async () => await (await subscriptionTask).DisposeAsync());
            }
        }

        static async Task WaitForRequestToBeCollected(
            Uri endpoint,
            RequestMessage request,
            RedisPendingRequest redisPending,
            IHalibutRedisTransport halibutRedisTransport,
            TimeSpan timeBetweenCheckingIfRequestWasCollected,
            ILog log,
            CancellationToken cancellationToken)
        {
            log = log.ForContext<TimerBasedNodeHeartBeatSender>();
            log.Write(EventType.Diagnostic, "Waiting for request {0} to be collected from queue", request.ActivityId);
            
            while (!cancellationToken.IsCancellationRequested)
            {
                await Try.IgnoringError(async () =>
                {
                    await Task.WhenAny(
                        TimerBasedDelay.Delay(timeBetweenCheckingIfRequestWasCollected, cancellationToken),
                        redisPending.WaitForRequestToBeMarkedAsCollected(cancellationToken));
                });
                
                if(cancellationToken.IsCancellationRequested) break;
                
                try
                {
                    // Has something else determined the request was collected?
                    if (redisPending.HasRequestBeenMarkedAsCollected)
                    {
                        log.Write(EventType.Diagnostic, "Request {0} has been marked as collected", request.ActivityId);
                        return;
                    }

                    // Check ourselves if the request has been collected.
                    var requestIsStillOnQueue = await halibutRedisTransport.IsRequestStillOnQueue(endpoint, request.ActivityId, cancellationToken);
                    if (!requestIsStillOnQueue)
                    {
                        log.Write(EventType.Diagnostic, "Request {0} is no longer on queue", request.ActivityId);
                        await redisPending.RequestHasBeenCollectedAndWillBeTransferred();
                        return;
                    }
                }
                catch (Exception ex)
                {
                    log.WriteException(EventType.Diagnostic, "Error checking if request {0} is still on queue", ex, request.ActivityId);
                }
            }
            
            log.Write(EventType.Diagnostic, "Stopped waiting for request {0} to be collected (cancelled)", request.ActivityId);
        }
    }
}
#endif
```

- [ ] **Step 2: Build**

Run: `dotnet build source/Halibut/Halibut.csproj -f net8.0`
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add source/Halibut/Queue/Redis/NodeHeartBeat/TimerBasedNodeHeartBeatWatcher.cs
git commit -m "Add TimerBasedNodeHeartBeatWatcher, a Timer-based copy of NodeHeartBeatWatcher"
```

---

### Task 6: Add `TimerBasedRedisPendingRequestQueue`

Literal copy of `RedisPendingRequestQueue.cs`, wired to the Timer-based heartbeat sender/watcher and `TimerBasedDelay`, implementing the shared `IRedisPendingRequestQueueTestControls` interface from Task 2.

**Files:**
- Create: `source/Halibut/Queue/Redis/TimerBasedRedisPendingRequestQueue.cs`
- Test: `source/Halibut.Tests/Queue/Redis/TimerBasedRedisPendingRequestQueueSmokeFixture.cs`

**Interfaces:**
- Consumes: `Halibut.Queue.Redis.WatcherAndDisposables` (Task 1), `Halibut.Queue.Redis.IRedisPendingRequestQueueTestControls` (Task 2), `Halibut.Util.TimerBasedDelay.Delay` (Task 3), `Halibut.Queue.Redis.NodeHeartBeat.TimerBasedNodeHeartBeatSender` (Task 4), `Halibut.Queue.Redis.NodeHeartBeat.TimerBasedNodeHeartBeatWatcher` (Task 5).
- Produces: `Halibut.Queue.Redis.TimerBasedRedisPendingRequestQueue : IRedisPendingRequestQueueTestControls, IDisposable` — constructor identical in shape to `RedisPendingRequestQueue(Uri, IWatchForRedisLosingAllItsData, ILog, IHalibutRedisTransport, IMessageSerialiserAndDataStreamStorage, HalibutTimeoutsAndLimits)`.

- [ ] **Step 1: Write the failing test**

Create `source/Halibut.Tests/Queue/Redis/TimerBasedRedisPendingRequestQueueSmokeFixture.cs`:

```csharp

#if NET8_0_OR_GREATER
using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Queue.Redis;
using Halibut.Queue.Redis.MessageStorage;
using Halibut.Queue.Redis.RedisDataLossDetection;
using Halibut.Queue.Redis.RedisHelpers;
using Halibut.Tests.Builders;
using Halibut.Tests.Queue.Redis.Utils;
using Halibut.Tests.Support.Logging;
using Halibut.Tests.TestSetup.Redis;
using Halibut.Logging;
using NUnit.Framework;

namespace Halibut.Tests.Queue.Redis
{
    [RedisTest]
    public class TimerBasedRedisPendingRequestQueueSmokeFixture : BaseTest
    {
        [Test]
        public async Task DequeueAsync_ShouldReturnRequestFromRedis()
        {
            var endpoint = new Uri("poll://" + Guid.NewGuid());
            await using var redisFacade = RedisFacadeBuilder.CreateRedisFacade();
            var redisTransport = new HalibutRedisTransport(redisFacade);
            var request = new RequestMessageBuilder("poll://test-endpoint").Build();
            var log = new TestContextLogCreator("Queue", LogLevel.Trace).CreateNewForPrefix("");

            var messageSerializer = new QueueMessageSerializerBuilder().Build();
            var dataStreamStore = new InMemoryStoreDataStreamsForDistributedQueues();
            var messageReaderWriter = new MessageSerialiserAndDataStreamStorage(messageSerializer, dataStreamStore);

            var sut = new TimerBasedRedisPendingRequestQueue(endpoint, new RedisNeverLosesData(), log, redisTransport, messageReaderWriter, new HalibutTimeoutsAndLimits());
            await sut.WaitUntilQueueIsSubscribedToReceiveMessages();

            var task = sut.QueueAndWaitAsync(request, CancellationToken.None);

            var result = await sut.DequeueAsync(CancellationToken);

            result.Should().NotBeNull();
            result!.RequestMessage.Id.Should().Be(request.Id);
        }
    }
}
#endif
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test source/Halibut.Tests/Halibut.Tests.csproj -f net8.0 --filter "FullyQualifiedName~TimerBasedRedisPendingRequestQueueSmokeFixture"`
Expected: FAIL to build with `error CS0246: The type or namespace name 'TimerBasedRedisPendingRequestQueue' could not be found`.

- [ ] **Step 3: Implement `TimerBasedRedisPendingRequestQueue`**

Create `source/Halibut/Queue/Redis/TimerBasedRedisPendingRequestQueue.cs`:

```csharp

#if NET8_0_OR_GREATER
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Queue.QueuedDataStreams;
using Halibut.Queue.Redis.Cancellation;
using Halibut.Queue.Redis.Exceptions;
using Halibut.Queue.Redis.MessageStorage;
using Halibut.Queue.Redis.NodeHeartBeat;
using Halibut.Queue.Redis.RedisDataLossDetection;
using Halibut.Queue.Redis.RedisHelpers;
using Halibut.Queue.Redis.ResponseMessageTransfer;
using Halibut.ServiceModel;
using Halibut.Transport.Protocol;
using Halibut.Util;
using Nito.AsyncEx;
using StackExchange.Redis;

namespace Halibut.Queue.Redis
{
    class TimerBasedRedisPendingRequestQueue : IRedisPendingRequestQueueTestControls, IDisposable
    {
        readonly Uri endpoint;
        readonly IWatchForRedisLosingAllItsData watchForRedisLosingAllItsData;
        readonly ILog log;
        readonly IHalibutRedisTransport halibutRedisTransport;
        readonly HalibutTimeoutsAndLimits halibutTimeoutsAndLimits;
        readonly IMessageSerialiserAndDataStreamStorage messageSerialiserAndDataStreamStorage;
        readonly AsyncManualResetEvent hasItemsForEndpoint = new();

        readonly CancelOnDisposeCancellationToken queueCts = new ();
        internal ConcurrentDictionary<Guid, WatcherAndDisposables> DisposablesForInFlightRequests = new();
        
        readonly CancellationToken queueToken;
        
        // Used for testing.
        int numberOfInFlightRequestsThatHaveReachedTheStageOfBeingReadyForCollection = 0;

        Task<IAsyncDisposable> RequestMessageAvailablePulseChannelSubscriberDisposer { get; }
        
        public bool IsEmpty => Count == 0;
        public int Count => numberOfInFlightRequestsThatHaveReachedTheStageOfBeingReadyForCollection;

        // The timespan is more generous for the sender going offline, since if it does go offline,
        // under some cases the request completing is advantageous. That node needs to
        // re-do the entire RPC for idempotent RPCs this might mean that the task required is already done.
        internal TimeSpan RequestSenderNodeHeartBeatTimeout { get; set; }  = TimeSpan.FromSeconds(90);
        
        // How often the Request Sender sends a heart beat.
        internal TimeSpan RequestSenderNodeHeartBeatRate { get; set; }  = TimeSpan.FromSeconds(15);
        
        /// <summary>
        /// The amount of time since the last heart beat from the node sending the request to Tentacle
        /// before the node is assumed to be offline.
        ///
        /// Setting this too high means things above the RPC might not have time to retry.
        /// </summary>
        public TimeSpan RequestReceiverNodeHeartBeatTimeout { get; set; } = TimeSpan.FromSeconds(60);
        
        // How often the Request Receiver node sends a heart beat.
        internal TimeSpan RequestReceiverNodeHeartBeatRate { get; set; }  = TimeSpan.FromSeconds(15);
        
        // How long the response message can live in redis.
        internal TimeSpan TTLOfResponseMessage { get; set; } = TimeSpan.FromMinutes(20);
        
        internal TimeSpan TimeBetweenCheckingIfRequestWasCollected { get; set; } = TimeSpan.FromSeconds(15);
        
        /// <summary>
        /// How long to delay before we will start sending or checking for heart beat pulses.
        /// Note that the Node Sending the request won't send heart beats until it has detected
        /// the request has been collected. Which won't be detected until TimeBetweenCheckingIfRequestWasCollected
        /// has passed.
        ///
        /// 7s is chosen since Tentacle Client has a 5s wait for script delay, and 7s just exceeds that. This
        /// should be enough time that most of the time we never need to send or check for heart beat messages
        /// reducing load on the queue.
        /// </summary>
        static readonly HeartBeatInitialDelay DefaultHeartBeatInitialDelay = new(TimeSpan.FromSeconds(7));
        public HeartBeatInitialDelay HeartBeatInitialDelay { get; set; } = DefaultHeartBeatInitialDelay;
        
        /// <summary>
        /// With this delay short requests (sub 5s) may not be cancelled if the cancellation occurs after the request
        /// has been collected by the other side.
        /// Note that Halibut's cancellation does NOT mean that the other side will stop processing the request once
        /// cancellation is sent. instead all it can do is terminate the network stream. Which really means,
        /// we can only stop transferring of a request or a response.
        /// Thus setting this to a value greater than zero will have very little impact since, it is very unlikely
        /// we could have stopped the request from being executed by the service anyway.
        /// 7 seconds is chosen since, for our use cases most requests are done within that time range.
        /// </summary>
        static readonly DelayBeforeSubscribingToRequestCancellation DefaultDelayBeforeSubscribingToRequestCancellation = new(TimeSpan.FromSeconds(7));
        public DelayBeforeSubscribingToRequestCancellation DelayBeforeSubscribingToRequestCancellation { get; set; } = DefaultDelayBeforeSubscribingToRequestCancellation;

        TimeSpan IRedisPendingRequestQueueTestControls.RequestSenderNodeHeartBeatTimeout
        {
            get => RequestSenderNodeHeartBeatTimeout;
            set => RequestSenderNodeHeartBeatTimeout = value;
        }

        TimeSpan IRedisPendingRequestQueueTestControls.RequestSenderNodeHeartBeatRate
        {
            get => RequestSenderNodeHeartBeatRate;
            set => RequestSenderNodeHeartBeatRate = value;
        }

        TimeSpan IRedisPendingRequestQueueTestControls.RequestReceiverNodeHeartBeatRate
        {
            get => RequestReceiverNodeHeartBeatRate;
            set => RequestReceiverNodeHeartBeatRate = value;
        }

        TimeSpan IRedisPendingRequestQueueTestControls.TimeBetweenCheckingIfRequestWasCollected
        {
            get => TimeBetweenCheckingIfRequestWasCollected;
            set => TimeBetweenCheckingIfRequestWasCollected = value;
        }

        ConcurrentDictionary<Guid, WatcherAndDisposables> IRedisPendingRequestQueueTestControls.DisposablesForInFlightRequests => DisposablesForInFlightRequests;

        Task IRedisPendingRequestQueueTestControls.WaitUntilQueueIsSubscribedToReceiveMessages() => WaitUntilQueueIsSubscribedToReceiveMessages();
        
        public TimerBasedRedisPendingRequestQueue(
            Uri endpoint, 
            IWatchForRedisLosingAllItsData watchForRedisLosingAllItsData,
            ILog log, 
            IHalibutRedisTransport halibutRedisTransport, 
            IMessageSerialiserAndDataStreamStorage messageSerialiserAndDataStreamStorage, 
            HalibutTimeoutsAndLimits halibutTimeoutsAndLimits)
        {
            this.endpoint = endpoint;
            this.watchForRedisLosingAllItsData = watchForRedisLosingAllItsData;
            this.log = log.ForContext<TimerBasedRedisPendingRequestQueue>();
            this.messageSerialiserAndDataStreamStorage = messageSerialiserAndDataStreamStorage;
            this.halibutRedisTransport = halibutRedisTransport;
            this.halibutTimeoutsAndLimits = halibutTimeoutsAndLimits;
            this.queueToken = queueCts.Token;
            
            // Ideally we would only subscribe subscribers which are using this queue.
            RequestMessageAvailablePulseChannelSubscriberDisposer = Task.Run(async () => await this.halibutRedisTransport.SubscribeToRequestMessagePulseChannel(endpoint, _ => hasItemsForEndpoint.Set(), queueToken));
        }

        internal async Task WaitUntilQueueIsSubscribedToReceiveMessages() => await RequestMessageAvailablePulseChannelSubscriberDisposer;

        async Task<CancellationToken> DataLossCancellationToken(CancellationToken? cancellationToken)
        {
            // Try to get the token immediately if monitoring is already active
            var token = watchForRedisLosingAllItsData.TryGetTokenForDataLossDetection();
            if (token.HasValue)
            {
                return token.Value;
            }
            
            // Fall back to waiting for the token if not immediately available
            await using var cts = new CancelOnDisposeCancellationToken(queueCts.Token, cancellationToken ?? CancellationToken.None);
            return await watchForRedisLosingAllItsData.GetTokenForDataLossDetection(TimeSpan.FromSeconds(30), cts.Token);
        }

        public async Task<ResponseMessage> QueueAndWaitAsync(RequestMessage request, CancellationToken requestCancellationToken)
        {
            CancellationToken dataLossCt;
            try
            {
                dataLossCt = await DataLossCancellationToken(requestCancellationToken);
            }
            catch (Exception ex)
            {
                if (requestCancellationToken.IsCancellationRequested) throw RedisPendingRequest.CreateExceptionForRequestWasCancelledBeforeCollected(request, log);
                throw new CouldNotGetDataLossTokenInTimeHalibutClientException("Unable to reconnect to redis to get data loss detection CT", ex);
            }

            Exception? CancellationReason()
            {
                if (dataLossCt.IsCancellationRequested) return new RedisDataLossHalibutClientException($"Request {request.ActivityId} was cancelled because we detected that redis lost all of its data.");
                if (queueToken.IsCancellationRequested) return new RedisQueueShutdownClientException($"Request {request.ActivityId} was cancelled because the queue is shutting down.");
                return null;
            }

            Exception? CreateCancellationExceptionIfCancelled()
            {
                if (requestCancellationToken.IsCancellationRequested) return RedisPendingRequest.CreateExceptionForRequestWasCancelledBeforeCollected(request, log);
                return CancellationReason();
            }
            

            await using var cts = new CancelOnDisposeCancellationToken(queueCts.Token, requestCancellationToken, dataLossCt);
            var cancellationToken = cts.Token;
            
            using var pending = new RedisPendingRequest(request, log);

            RedisStoredMessage messageToStore;
            HeartBeatDrivenDataStreamProgressReporter heartBeatDrivenDataStreamProgressReporter;
            try
            {
                (messageToStore, heartBeatDrivenDataStreamProgressReporter) = await messageSerialiserAndDataStreamStorage.PrepareRequest(request, cancellationToken);
            }
            catch (Exception ex)
            {
                throw CreateCancellationExceptionIfCancelled() 
                      ?? new ErrorWhilePreparingRequestForQueueHalibutClientException($"Request {request.ActivityId} failed since an error occured when preparing request for queue", ex);
            }
            await using var _ = heartBeatDrivenDataStreamProgressReporter; // Disposal of the reporter notifies all DataStream progress reportors that the upload is complete.
            
            
            // Start listening for a response to the request, we don't want to miss the response.
            await using var pollAndSubscribeToResponse = new PollAndSubscribeToResponse(endpoint, request.ActivityId, halibutRedisTransport, log);

            var tryClearRequestFromQueueAtMostOnce = new AsyncLazy<bool>(async () => await TryClearRequestFromQueue(pending));
            try
            {
                await using var senderPulse = new TimerBasedNodeHeartBeatSender(endpoint, request.ActivityId, halibutRedisTransport, log, HalibutQueueNodeSendingPulses.RequestSenderNode, () => new HeartBeatMessage(), RequestSenderNodeHeartBeatRate, HeartBeatInitialDelay);
                // Make the request available before we tell people it is available.
                try
                {
                    await halibutRedisTransport.PutRequest(endpoint, request.ActivityId, messageToStore, request.Destination.PollingRequestQueueTimeout, cancellationToken);
                    await halibutRedisTransport.PushRequestGuidOnToQueue(endpoint, request.ActivityId, cancellationToken);
                    await halibutRedisTransport.PulseRequestPushedToEndpoint(endpoint, cancellationToken);
                }
                catch (Exception ex)
                {
                    throw CreateCancellationExceptionIfCancelled() 
                          ?? new ErrorOccuredWhenInsertingDataIntoRedisHalibutPendingRequestQueueHalibutClientException($"Request {request.ActivityId} failed since an error occured inserting the data into the queue", ex);
                }

                Interlocked.Increment(ref numberOfInFlightRequestsThatHaveReachedTheStageOfBeingReadyForCollection);
                try
                {
                    // We must be careful here to ensure we will always return.
                    
                    var watchProcessingNodeStillHasHeartBeat = WatchProcessingNodeIsStillConnectedInBackground(request, pending, heartBeatDrivenDataStreamProgressReporter, cancellationToken);
                    var waitingForResponse = WaitForResponse(pollAndSubscribeToResponse, request, cancellationToken);
                    var pendingRequestWaitUntilComplete = pending.WaitUntilComplete(
                        async () => await tryClearRequestFromQueueAtMostOnce.Task,
                        CancellationReason,
                        cancellationToken);
                    
                    cts.AwaitTasksBeforeCTSDispose(watchProcessingNodeStillHasHeartBeat, waitingForResponse, pendingRequestWaitUntilComplete);
                    
                    await Task.WhenAny(waitingForResponse, pendingRequestWaitUntilComplete, watchProcessingNodeStillHasHeartBeat);

                    if (pendingRequestWaitUntilComplete.IsCompleted || cancellationToken.IsCancellationRequested)
                    {
                        await pendingRequestWaitUntilComplete;
                        return pending.Response!;
                    }
                    
                    if (waitingForResponse.IsCompleted)
                    {
                        var response = await waitingForResponse;
                        if (response != null)
                        {
                            return await pending.SetResponse(response);
                        }
                        else if(!cancellationToken.IsCancellationRequested)
                        {
                            // We are no longer waiting for a response and have no response.
                            // The cancellation token has not been set so the request is not going to be cancelled.
                            // It is unclear how we got into this state, but lets at least error out.
                            return await pending.SetResponse(ResponseMessage.FromError(request, "Queue unexpectedly stopped waiting for a response"));
                        }
                    }
                    
                    if (watchProcessingNodeStillHasHeartBeat.IsCompleted)
                    {
                        var watcherResult = await watchProcessingNodeStillHasHeartBeat;
                        if (watcherResult == NodeWatcherResult.NodeMayHaveDisconnected)
                        {
                            // Make a list ditch effort to check if a response exists now.
                            if (await pollAndSubscribeToResponse.TryGetResponseFromRedis("Watcher", cancellationToken))
                            {
                                var response = await waitingForResponse;
                                if (response != null)
                                {
                                    return await pending.SetResponse(response);
                                }
                            }
                            
                            return await pending.SetResponse(ResponseMessage.FromError(request, "The node processing the request did not send a heartbeat for long enough, and so the node is now assumed to be offline."));
                        }
                    }

                    return await pending.SetResponse(ResponseMessage.FromError(request, "Impossible queue state reached"));
                }
                finally
                {
                    Interlocked.Decrement(ref numberOfInFlightRequestsThatHaveReachedTheStageOfBeingReadyForCollection);
                }
            }
            finally
            {
                InBackgroundSendCancellationIfRequestWasCancelled(request, pending);
                // Make an attempt to ensure the request is removed from redis, if we are unsure it was removed.
                var background = Task.Run(async () => await Try.IgnoringError(async () =>
                {
                    if (pending.HasRequestBeenMarkedAsCollected
                        || !pollAndSubscribeToResponse.ResponseJson.IsCompletedSuccessfully)
                    {
                        await tryClearRequestFromQueueAtMostOnce.Task;
                    }
                }));
            }
        }

        
        void InBackgroundSendCancellationIfRequestWasCancelled(RequestMessage request, RedisPendingRequest redisPending)
        {
            if (redisPending.PendingRequestCancellationToken.IsCancellationRequested)
            {
                log.Write(EventType.Diagnostic, "Request {0} was cancelled, sending cancellation to endpoint {1}", request.ActivityId, endpoint);
                Task.Run(async () => await RequestCancelledSender.TrySendCancellation(halibutRedisTransport, endpoint, request, log));
            }
            else
            {
                log.Write(EventType.Diagnostic, "Request {0} was not cancelled, no cancellation needed for endpoint {1}", request.ActivityId, endpoint);
            }
        }

        async Task<NodeWatcherResult?> WatchProcessingNodeIsStillConnectedInBackground(RequestMessage request, RedisPendingRequest redisPendingRequest, IGetNotifiedOfHeartBeats notifiedOfHeartBeats, CancellationToken cancellationToken)
        {
            await Task.Yield();
            
            return await TimerBasedNodeHeartBeatWatcher.WatchThatNodeProcessingTheRequestIsStillAlive(
                endpoint,
                request,
                redisPendingRequest,
                halibutRedisTransport,
                TimeBetweenCheckingIfRequestWasCollected,
                log,
                RequestReceiverNodeHeartBeatTimeout,
                notifiedOfHeartBeats,
                cancellationToken);
        }

        async Task<bool> TryClearRequestFromQueue(RedisPendingRequest redisPending)
        {
            var request = redisPending.Request;
            log.Write(EventType.Diagnostic, "Attempting to clear request {0} from queue for endpoint {1}", request.ActivityId, endpoint);
            
            // The time the message is allowed to sit on the queue for has elapsed.
            // Let's try to pop if from the queue, either:
            // - We pop it, which means it was never collected so let pending deal with the timeout.
            // - We could not pop it, which means it was collected.
            try
            {
                if (redisPending.HasRequestBeenMarkedAsCollected)
                {
                    log.Write(EventType.Diagnostic, "Request {0} has already been marked as collected, skipping queue removal for endpoint {1}", request.ActivityId, endpoint);
                    return false;
                }
                await using var cts = new CancelOnDisposeCancellationToken();
                cts.CancelAfter(TimeSpan.FromMinutes(2)); // Best efforts.
                var requestMessage = await halibutRedisTransport.TryGetAndRemoveRequest(endpoint, request.ActivityId, cts.Token);
                if (requestMessage != null)
                {
                    log.Write(EventType.Diagnostic, "Successfully removed request {0} from queue - request was never collected by a processing node", request.ActivityId);
                    return true;
                }
                else
                {
                    await redisPending.RequestHasBeenCollectedAndWillBeTransferred();
                    log.Write(EventType.Diagnostic, "Request {0} was not found in queue - it was already collected by a processing node", request.ActivityId);
                }
            }
            catch (Exception ex)
            {
                log.WriteException(EventType.Error, "Failed to clear request {0} from queue for endpoint {1}", ex, request.ActivityId, endpoint);
            }
            return false;
        }
        
        async Task<ResponseMessage?> WaitForResponse(
            PollAndSubscribeToResponse pollAndSubscribeToResponse,
            RequestMessage requestMessage,
            CancellationToken cancellationToken)
        {
            await Task.Yield();
            var activityId = requestMessage.ActivityId;
            RedisStoredMessage responseJson;
            try
            {
                log.Write(EventType.Diagnostic, "Waiting for response for request {0}", activityId);
                responseJson = await pollAndSubscribeToResponse.ResponseJson.WaitAsync(cancellationToken);
                log.Write(EventType.Diagnostic, "Received response JSON for request {0}, deserializing", activityId);
            }
            catch (Exception ex)
            {
                log.WriteException(EventType.Error, "Error while processing response for request {0}", ex, activityId);
                return null;
            }

            try
            {
                var response = await messageSerialiserAndDataStreamStorage.ReadResponse(responseJson, cancellationToken);
                log.Write(EventType.Diagnostic, "Successfully deserialized response for request {0}", activityId);
                return response;
            }
            catch (Exception ex)
            {
                log.Write(EventType.Error, "Error deserializing response for request {0}", activityId);
                return ResponseMessage.FromException(requestMessage, new Exception("Error occured when reading data from the queue", ex));
            }
        }
        
        public async Task<RequestMessageWithCancellationToken?> DequeueAsync(CancellationToken cancellationToken)
        {
            // Is it good or bad that redis exceptions will bubble out of here?
            // It will kill the TCP connection, which will force re-connect (in perhaps a backoff function)
            // This could result in connecting to a node that is actually connected to redis. It could also
            // cause a cascade of failure from high load.
            var pending = await DequeueNextAsync(cancellationToken);
            if (pending == null) return null;

            var pendingRequest = pending.Value.Item1;
            var dataStreamsTransferProgress = pending.Value.Item2;
            
            var disposables = new DisposableCollection();
            try
            {
                // There is a chance the data loss occured after we got the data but before here.
                // In that case we will just time out because of the lack of heart beats.
                var dataLossCT = await DataLossCancellationToken(cancellationToken);
                
                disposables.AddAsyncDisposable(new TimerBasedNodeHeartBeatSender(
                    endpoint,
                    pendingRequest.ActivityId,
                    halibutRedisTransport,
                    log,
                    HalibutQueueNodeSendingPulses.RequestProcessorNode,
                    () => HeartBeatMessage.Build(dataStreamsTransferProgress),
                    RequestReceiverNodeHeartBeatRate,
                    HeartBeatInitialDelay));
                var watcher = new WatchForRequestCancellationOrSenderDisconnect(endpoint, pendingRequest.ActivityId, halibutRedisTransport, RequestSenderNodeHeartBeatTimeout, HeartBeatInitialDelay, DelayBeforeSubscribingToRequestCancellation, log);
                disposables.AddAsyncDisposable(watcher);
                
                var cts = new CancelOnDisposeCancellationToken(watcher.RequestProcessingCancellationToken, dataLossCT);
                disposables.AddAsyncDisposable(cts);
                
                var response = new RequestMessageWithCancellationToken(pendingRequest, cts.Token);
                DisposablesForInFlightRequests[pendingRequest.ActivityId] = new WatcherAndDisposables(disposables, cts.Token, watcher);
                return response;
            }
            catch (Exception)
            {
                await Try.IgnoringError(async () => await disposables.DisposeAsync());
                throw;
            }
        }

        public const string RequestAbandonedMessage = "The request was abandoned, possibly because the node processing the request shutdown or redis lost all of its data.";
        
        public async Task ApplyResponse(ResponseMessage response, Guid requestActivityId)
        {
            log.Write(EventType.MessageExchange, "Applying response for request {0}", requestActivityId);
            WatcherAndDisposables? watcherAndDisposables = null;
            if (!DisposablesForInFlightRequests.TryRemove(requestActivityId, out watcherAndDisposables))
            {
                log.Write(EventType.Diagnostic, "No in-flight request resources found to dispose for request {0}", requestActivityId);
            }
            
            try
            {
                if (response == null) 
                {
                    log.Write(EventType.Diagnostic, "Response is null for request {0}, skipping apply", requestActivityId);
                    return;
                }

                log.Write(EventType.MessageExchange, "Preparing response payload for request {0}", requestActivityId);
                var cancellationToken = CancellationToken.None;

                // This node has now completed the RPC, and so the response must be sent
                // back to the node which sent the response

                if (watcherAndDisposables != null && watcherAndDisposables.RequestCancelledForAnyReasonCancellationToken.IsCancellationRequested)
                {
                    if (!watcherAndDisposables.Watcher.SenderCancelledTheRequest)
                    {
                        log.Write(EventType.Diagnostic, "Response for request {0}, has been overridden with an abandon message as the request was abandoned", requestActivityId);
                        response = ResponseMessage.FromException(response, new HalibutClientException(RequestAbandonedMessage));
                    }
                }
                var responseStoredMessage = await messageSerialiserAndDataStreamStorage.PrepareResponse(response, cancellationToken);
                log.Write(EventType.MessageExchange, "Sending response message for request {0}", requestActivityId);
                await ResponseMessageSender.SendResponse(halibutRedisTransport, endpoint, requestActivityId, responseStoredMessage, TTLOfResponseMessage, log);
                log.Write(EventType.MessageExchange, "Successfully applied response for request {0}", requestActivityId);
            }
            catch (Exception ex)
            {
                log.WriteException(EventType.Error, "Error applying response for request {0}", ex, requestActivityId);
                throw;
            }
            finally
            {
                log.Write(EventType.Diagnostic, "Disposing in-flight request resources for request {0}", requestActivityId);
                if (watcherAndDisposables != null)
                {
                    await watcherAndDisposables.DisposeAsync();
                }
            }
        }

        async Task<(RequestMessage, RequestDataStreamsTransferProgress)?> DequeueNextAsync(CancellationToken cancellationToken)
        {
            await using var cts = new CancelOnDisposeCancellationToken(queueToken, cancellationToken);
            try
            {
                hasItemsForEndpoint.Reset();

                var first = await TryRemoveNextItemFromQueue(cts.Token);
                if (first != null)
                {
                    return first;
                }

                await Task.WhenAny(
                    hasItemsForEndpoint.WaitAsync(cts.Token),
                    TimerBasedDelay.Delay(halibutTimeoutsAndLimits.PollingQueueWaitTimeout, cts.Token));

                if (!hasItemsForEndpoint.IsSet)
                {
                    // Timed out waiting for something to go on the queue, send back a null to tentacle
                    // to keep the connection healthy.
                    return null;
                }

                return await TryRemoveNextItemFromQueue(cts.Token);
            }
            catch (Exception ex)
            {
                if (!queueToken.IsCancellationRequested)
                {
                    // If redis is down, don't log when dequeuing work since every polling service will log the issue every ~30s.
                    if (ex is not RedisConnectionException)
                    {
                        log.WriteException(EventType.Error, "Error occured dequeuing from the queue", ex);
                    }
                    
                    // It is very likely a queue error means every tentacle will return an error.
                    // Add a random delay to help avoid every client coming back at exactly the same time.
                    await TimerBasedDelay.Delay(TimeSpan.FromSeconds(new Random().Next(halibutTimeoutsAndLimits.PollingQueueWaitTimeout.Seconds)), cts.Token);
                }
                throw;
            }
            finally
            {
                await cts.CancelAsync();
            }
        }

        async Task<(RequestMessage, RequestDataStreamsTransferProgress)?> TryRemoveNextItemFromQueue(CancellationToken cancellationToken)
        {
            while (true)
            {
                var activityId = await halibutRedisTransport.TryPopNextRequestGuid(endpoint, cancellationToken);

                if (activityId is null)
                {
                    // Nothing is on the queue.
                    return null;
                }
                
                var jsonRequest = await halibutRedisTransport.TryGetAndRemoveRequest(endpoint, activityId.Value, cancellationToken);

                if (jsonRequest == null)
                {
                    // This request has been picked up by someone else, go around the loop and look for something else to do.
                    continue;
                }

                var request = await messageSerialiserAndDataStreamStorage.ReadRequest(jsonRequest, cancellationToken);
                log.Write(EventType.Diagnostic, "Successfully collected request {0} from queue for endpoint {1}", request.Item1.ActivityId, endpoint);

                return request;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await Try.IgnoringError(async () => await queueCts.DisposeAsync());
            await Try.IgnoringError(async () => await (await RequestMessageAvailablePulseChannelSubscriberDisposer).DisposeAsync());
        }

        public void Dispose()
        {
            DisposeAsync().GetAwaiter().GetResult();
        }
    }
}
#endif
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test source/Halibut.Tests/Halibut.Tests.csproj -f net8.0 --filter "FullyQualifiedName~TimerBasedRedisPendingRequestQueueSmokeFixture"`
Expected: PASS (requires local Redis).

- [ ] **Step 5: Commit**

```bash
git add source/Halibut/Queue/Redis/TimerBasedRedisPendingRequestQueue.cs source/Halibut.Tests/Queue/Redis/TimerBasedRedisPendingRequestQueueSmokeFixture.cs
git commit -m "Add TimerBasedRedisPendingRequestQueue, a Timer-based copy of RedisPendingRequestQueue"
```

---

### Task 7: Static switch on `RedisPendingRequestQueueFactory`

Adds the enum + static switch, branches `CreateQueue`, and fixes the two existing test helpers that hard-cast the factory's return value to the concrete `RedisPendingRequestQueue` type (they'd throw `InvalidCastException` the moment `Implementation` is set to `Timer`).

**Files:**
- Create: `source/Halibut/Queue/Redis/RedisPendingRequestQueueImplementation.cs`
- Modify: `source/Halibut/Queue/Redis/RedisPendingRequestQueueFactory.cs`
- Modify: `source/Halibut.Tests/Queue/Redis/Utils/TestRedisPendingRequestQueueFactory.cs`
- Modify: `source/Halibut.Tests/Queue/Redis/MessageSerialiserAndDataStreamStorageExceptionObserverTest.cs`
- Test: `source/Halibut.Tests/Queue/Redis/RedisPendingRequestQueueFactoryFixture.cs`

**Interfaces:**
- Consumes: `Halibut.Queue.Redis.TimerBasedRedisPendingRequestQueue` (Task 6), `Halibut.Queue.Redis.IRedisPendingRequestQueueTestControls` (Task 2).
- Produces: `Halibut.Queue.Redis.RedisPendingRequestQueueImplementation` enum (`TaskDelay`, `Timer`); `RedisPendingRequestQueueFactory.Implementation` static property (`RedisPendingRequestQueueImplementation`, default `TaskDelay`).

- [ ] **Step 1: Write the failing test**

Create `source/Halibut.Tests/Queue/Redis/RedisPendingRequestQueueFactoryFixture.cs`:

```csharp

#if NET8_0_OR_GREATER
using System;
using FluentAssertions;
using Halibut.Queue.Redis;
using Halibut.Queue.Redis.MessageStorage;
using Halibut.Queue.Redis.RedisDataLossDetection;
using Halibut.Queue.Redis.RedisHelpers;
using Halibut.Tests.Queue.Redis.Utils;
using Halibut.Tests.Support.Logging;
using Halibut.Tests.TestSetup.Redis;
using Halibut.Logging;
using NUnit.Framework;

namespace Halibut.Tests.Queue.Redis
{
    [RedisTest]
    public class RedisPendingRequestQueueFactoryFixture : BaseTest
    {
        RedisPendingRequestQueueImplementation originalImplementation;

        [SetUp]
        public void SaveOriginalImplementation()
        {
            originalImplementation = RedisPendingRequestQueueFactory.Implementation;
        }

        [TearDown]
        public void RestoreOriginalImplementation()
        {
            RedisPendingRequestQueueFactory.Implementation = originalImplementation;
        }

        [Test]
        public async Task CreateQueue_DefaultsToTaskDelayImplementation()
        {
            await using var redisFacade = RedisFacadeBuilder.CreateRedisFacade();
            var factory = BuildFactory(redisFacade);

            var queue = factory.CreateQueue(new Uri("poll://" + Guid.NewGuid()));

            queue.Should().BeOfType<RedisPendingRequestQueue>();
        }

        [Test]
        public async Task CreateQueue_BuildsTimerBasedQueue_WhenImplementationIsTimer()
        {
            RedisPendingRequestQueueFactory.Implementation = RedisPendingRequestQueueImplementation.Timer;

            await using var redisFacade = RedisFacadeBuilder.CreateRedisFacade();
            var factory = BuildFactory(redisFacade);

            var queue = factory.CreateQueue(new Uri("poll://" + Guid.NewGuid()));

            queue.Should().BeOfType<TimerBasedRedisPendingRequestQueue>();
        }

        static RedisPendingRequestQueueFactory BuildFactory(RedisFacade redisFacade)
        {
            var messageSerializer = new QueueMessageSerializerBuilder().Build();
            var dataStreamStore = new InMemoryStoreDataStreamsForDistributedQueues();
            var logFactory = new TestContextLogCreator("Redis", LogLevel.Trace).ToCachingLogFactory();
            var redisTransport = new HalibutRedisTransport(redisFacade);
            var watchForRedisLosingAllItsData = new WatchForRedisLosingAllItsData(redisFacade, logFactory.ForPrefix("Redis"));

            return new RedisPendingRequestQueueFactory(messageSerializer, dataStreamStore, watchForRedisLosingAllItsData, redisTransport, new HalibutTimeoutsAndLimits(), logFactory);
        }
    }
}
#endif
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test source/Halibut.Tests/Halibut.Tests.csproj -f net8.0 --filter "FullyQualifiedName~RedisPendingRequestQueueFactoryFixture"`
Expected: FAIL to build with `error CS0117: 'RedisPendingRequestQueueFactory' does not contain a definition for 'Implementation'` (and `RedisPendingRequestQueueImplementation` not found).

- [ ] **Step 3: Add the enum**

Create `source/Halibut/Queue/Redis/RedisPendingRequestQueueImplementation.cs`:

```csharp
#if NET8_0_OR_GREATER
namespace Halibut.Queue.Redis
{
    public enum RedisPendingRequestQueueImplementation
    {
        TaskDelay,
        Timer
    }
}
#endif
```

- [ ] **Step 4: Add the static switch and branch `CreateQueue`**

In `source/Halibut/Queue/Redis/RedisPendingRequestQueueFactory.cs`, find:

```csharp
            this.exceptionObserver = exceptionObserver ?? NoOpMessageSerialiserAndDataStreamStorageExceptionObserver.Instance;
        }

        public IPendingRequestQueue CreateQueue(Uri endpoint)
        {
            var baseStorage = new MessageSerialiserAndDataStreamStorage(queueMessageSerializer, dataStreamStorage);
            var storageWithObserver = new MessageSerialiserAndDataStreamStorageWithExceptionObserver(baseStorage, exceptionObserver);
            
            var queue =  new RedisPendingRequestQueue(endpoint,
                watchForRedisLosingAllItsData,
                logFactory.ForEndpoint(endpoint),
                halibutRedisTransport,
                storageWithObserver,
                halibutTimeoutsAndLimits);

            if (queueDecorator != null) return queueDecorator(queue);
            
            return queue;
        }
```

Replace with:

```csharp
            this.exceptionObserver = exceptionObserver ?? NoOpMessageSerialiserAndDataStreamStorageExceptionObserver.Instance;
        }

        /// <summary>
        /// Selects which concrete IPendingRequestQueue implementation CreateQueue builds. Defaults to
        /// TaskDelay so production behaviour is unchanged unless something explicitly opts in to Timer.
        /// </summary>
        public static RedisPendingRequestQueueImplementation Implementation { get; set; } = RedisPendingRequestQueueImplementation.TaskDelay;

        public IPendingRequestQueue CreateQueue(Uri endpoint)
        {
            var baseStorage = new MessageSerialiserAndDataStreamStorage(queueMessageSerializer, dataStreamStorage);
            var storageWithObserver = new MessageSerialiserAndDataStreamStorageWithExceptionObserver(baseStorage, exceptionObserver);

            IPendingRequestQueue queue = Implementation == RedisPendingRequestQueueImplementation.Timer
                ? new TimerBasedRedisPendingRequestQueue(endpoint,
                    watchForRedisLosingAllItsData,
                    logFactory.ForEndpoint(endpoint),
                    halibutRedisTransport,
                    storageWithObserver,
                    halibutTimeoutsAndLimits)
                : new RedisPendingRequestQueue(endpoint,
                    watchForRedisLosingAllItsData,
                    logFactory.ForEndpoint(endpoint),
                    halibutRedisTransport,
                    storageWithObserver,
                    halibutTimeoutsAndLimits);

            if (queueDecorator != null) return queueDecorator(queue);
            
            return queue;
        }
```

- [ ] **Step 5: Fix `TestRedisPendingRequestQueueFactory` to not hard-cast to the concrete type**

In `source/Halibut.Tests/Queue/Redis/Utils/TestRedisPendingRequestQueueFactory.cs`, find:

```csharp
    public class TestRedisPendingRequestQueueFactory : IPendingRequestQueueFactory
    {
        RedisPendingRequestQueueFactory redisPendingRequestQueueFactory;
        List<Action<RedisPendingRequestQueue>> pendingRequestQueueCallBacks = new();

        internal TestRedisPendingRequestQueueFactory WithCallback(Action<RedisPendingRequestQueue> callback)
        {
            pendingRequestQueueCallBacks.Add(callback);
            return this;
        } 
        public TestRedisPendingRequestQueueFactory(RedisPendingRequestQueueFactory redisPendingRequestQueueFactory)
        {
            this.redisPendingRequestQueueFactory = redisPendingRequestQueueFactory;
        }

        public IPendingRequestQueue CreateQueue(Uri endpoint)
        {
            var queue = (RedisPendingRequestQueue) redisPendingRequestQueueFactory.CreateQueue(endpoint);
            foreach (var pendingRequestQueueCallBack in pendingRequestQueueCallBacks)
            {
                pendingRequestQueueCallBack(queue);
            }
            return queue;
        }
    }

    public static class RedisPendingRequestQueueFactoryExtensionMethods
    {
        public static TestRedisPendingRequestQueueFactory WithWaitForReceiverToBeReady(this RedisPendingRequestQueueFactory redisPendingRequestQueueFactory)
        {
            return redisPendingRequestQueueFactory
                .WithQueueCreationCallBack(queue => queue.WaitUntilQueueIsSubscribedToReceiveMessages().GetAwaiter().GetResult());
        }

        internal static TestRedisPendingRequestQueueFactory WithQueueCreationCallBack(this RedisPendingRequestQueueFactory redisPendingRequestQueueFactory, Action<RedisPendingRequestQueue> queueCreatedCallback)
        {
            return new TestRedisPendingRequestQueueFactory(redisPendingRequestQueueFactory)
                .WithCallback(queueCreatedCallback);
        }
        
        internal static TestRedisPendingRequestQueueFactory WithQueueCreationCallBack(this TestRedisPendingRequestQueueFactory redisPendingRequestQueueFactory, Action<RedisPendingRequestQueue> queueCreatedCallback)
        {
            return redisPendingRequestQueueFactory.WithCallback(queueCreatedCallback);
        }
    }
```

Replace with (every `RedisPendingRequestQueue` used as a type here becomes `IRedisPendingRequestQueueTestControls`, and the cast is widened accordingly — this is the type both `RedisPendingRequestQueue` and `TimerBasedRedisPendingRequestQueue` implement):

```csharp
    public class TestRedisPendingRequestQueueFactory : IPendingRequestQueueFactory
    {
        RedisPendingRequestQueueFactory redisPendingRequestQueueFactory;
        List<Action<IRedisPendingRequestQueueTestControls>> pendingRequestQueueCallBacks = new();

        internal TestRedisPendingRequestQueueFactory WithCallback(Action<IRedisPendingRequestQueueTestControls> callback)
        {
            pendingRequestQueueCallBacks.Add(callback);
            return this;
        } 
        public TestRedisPendingRequestQueueFactory(RedisPendingRequestQueueFactory redisPendingRequestQueueFactory)
        {
            this.redisPendingRequestQueueFactory = redisPendingRequestQueueFactory;
        }

        public IPendingRequestQueue CreateQueue(Uri endpoint)
        {
            var queue = (IRedisPendingRequestQueueTestControls) redisPendingRequestQueueFactory.CreateQueue(endpoint);
            foreach (var pendingRequestQueueCallBack in pendingRequestQueueCallBacks)
            {
                pendingRequestQueueCallBack(queue);
            }
            return queue;
        }
    }

    public static class RedisPendingRequestQueueFactoryExtensionMethods
    {
        public static TestRedisPendingRequestQueueFactory WithWaitForReceiverToBeReady(this RedisPendingRequestQueueFactory redisPendingRequestQueueFactory)
        {
            return redisPendingRequestQueueFactory
                .WithQueueCreationCallBack(queue => queue.WaitUntilQueueIsSubscribedToReceiveMessages().GetAwaiter().GetResult());
        }

        internal static TestRedisPendingRequestQueueFactory WithQueueCreationCallBack(this RedisPendingRequestQueueFactory redisPendingRequestQueueFactory, Action<IRedisPendingRequestQueueTestControls> queueCreatedCallback)
        {
            return new TestRedisPendingRequestQueueFactory(redisPendingRequestQueueFactory)
                .WithCallback(queueCreatedCallback);
        }
        
        internal static TestRedisPendingRequestQueueFactory WithQueueCreationCallBack(this TestRedisPendingRequestQueueFactory redisPendingRequestQueueFactory, Action<IRedisPendingRequestQueueTestControls> queueCreatedCallback)
        {
            return redisPendingRequestQueueFactory.WithCallback(queueCreatedCallback);
        }
    }
```

- [ ] **Step 6: Fix the other hard cast**

In `source/Halibut.Tests/Queue/Redis/MessageSerialiserAndDataStreamStorageExceptionObserverTest.cs`, find:

```csharp
            var sut = (RedisPendingRequestQueue)factory.CreateQueue(endpoint);
```

Replace with:

```csharp
            var sut = (IRedisPendingRequestQueueTestControls)factory.CreateQueue(endpoint);
```

(No `using` change needed — this file already has `using Halibut.Queue.Redis;` for `RedisPendingRequestQueueFactory`, and `IRedisPendingRequestQueueTestControls` lives in that same namespace.)

- [ ] **Step 7: Run test to verify it passes**

Run: `dotnet test source/Halibut.Tests/Halibut.Tests.csproj -f net8.0 --filter "FullyQualifiedName~RedisPendingRequestQueueFactoryFixture"`
Expected: PASS (2 tests).

- [ ] **Step 8: Run the two fixed-up fixtures to confirm they still pass**

Run: `dotnet test source/Halibut.Tests/Halibut.Tests.csproj -f net8.0 --filter "FullyQualifiedName~MessageSerialiserAndDataStreamStorageExceptionObserverTest"`
Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add source/Halibut/Queue/Redis/RedisPendingRequestQueueImplementation.cs source/Halibut/Queue/Redis/RedisPendingRequestQueueFactory.cs source/Halibut.Tests/Queue/Redis/Utils/TestRedisPendingRequestQueueFactory.cs source/Halibut.Tests/Queue/Redis/MessageSerialiserAndDataStreamStorageExceptionObserverTest.cs source/Halibut.Tests/Queue/Redis/RedisPendingRequestQueueFactoryFixture.cs
git commit -m "Add RedisPendingRequestQueueFactory.Implementation static switch between TaskDelay and Timer queues"
```

---

### Task 8: Wire `RedisPendingRequestQueueBuilder` to the static switch

`RedisPendingRequestQueueBuilder` is the test builder used by `[AllQueuesTestCases]` (via `PendingRequestQueueFixture`). It currently hardcodes `new RedisPendingRequestQueue(...)`; make it read the same static the production factory reads.

**Files:**
- Modify: `source/Halibut.Tests/Builders/RedisPendingRequestQueueBuilder.cs`

**Interfaces:**
- Consumes: `RedisPendingRequestQueueFactory.Implementation` (Task 7), `TimerBasedRedisPendingRequestQueue` (Task 6), `IRedisPendingRequestQueueTestControls` (Task 2).

- [ ] **Step 1: Update the builder**

In `source/Halibut.Tests/Builders/RedisPendingRequestQueueBuilder.cs`, find:

```csharp
            var disposableCollection = new DisposableCollection();

            var redisFacade = RedisFacadeBuilder.CreateRedisFacade(port: RedisTestHost.Port());
            disposableCollection.AddAsyncDisposable(redisFacade);
            
            var redisTransport = new HalibutRedisTransport(redisFacade);
            var dataStreamStore = new InMemoryStoreDataStreamsForDistributedQueues();
            var messageSerializer = new QueueMessageSerializerBuilder().Build();
            var messageReaderWriter = new MessageSerialiserAndDataStreamStorage(messageSerializer, dataStreamStore);

            var queue = new RedisPendingRequestQueue(endpoint, new RedisNeverLosesData(), log, redisTransport, messageReaderWriter, halibutTimeoutsAndLimits);
            if (defaultDelayBeforeSubscribingToRequestCancellation != null)
            {
                queue.DelayBeforeSubscribingToRequestCancellation = new DelayBeforeSubscribingToRequestCancellation(defaultDelayBeforeSubscribingToRequestCancellation.Value);
            }
            queue.WaitUntilQueueIsSubscribedToReceiveMessages().GetAwaiter().GetResult();
            
            return new QueueHolder(queue, disposableCollection);
```

Replace with:

```csharp
            var disposableCollection = new DisposableCollection();

            var redisFacade = RedisFacadeBuilder.CreateRedisFacade(port: RedisTestHost.Port());
            disposableCollection.AddAsyncDisposable(redisFacade);
            
            var redisTransport = new HalibutRedisTransport(redisFacade);
            var dataStreamStore = new InMemoryStoreDataStreamsForDistributedQueues();
            var messageSerializer = new QueueMessageSerializerBuilder().Build();
            var messageReaderWriter = new MessageSerialiserAndDataStreamStorage(messageSerializer, dataStreamStore);

            IRedisPendingRequestQueueTestControls queue = RedisPendingRequestQueueFactory.Implementation == RedisPendingRequestQueueImplementation.Timer
                ? new TimerBasedRedisPendingRequestQueue(endpoint, new RedisNeverLosesData(), log, redisTransport, messageReaderWriter, halibutTimeoutsAndLimits)
                : new RedisPendingRequestQueue(endpoint, new RedisNeverLosesData(), log, redisTransport, messageReaderWriter, halibutTimeoutsAndLimits);

            if (defaultDelayBeforeSubscribingToRequestCancellation != null)
            {
                queue.DelayBeforeSubscribingToRequestCancellation = new DelayBeforeSubscribingToRequestCancellation(defaultDelayBeforeSubscribingToRequestCancellation.Value);
            }
            queue.WaitUntilQueueIsSubscribedToReceiveMessages().GetAwaiter().GetResult();
            
            return new QueueHolder(queue, disposableCollection);
```

(`RedisPendingRequestQueueBuilder.cs` already has `using Halibut.Queue.Redis;` at the top, which covers `RedisPendingRequestQueueFactory`, `RedisPendingRequestQueueImplementation`, `TimerBasedRedisPendingRequestQueue`, and `IRedisPendingRequestQueueTestControls` — no using-statement changes needed.)

- [ ] **Step 2: Build**

Run: `dotnet build source/Halibut.Tests/Halibut.Tests.csproj -f net8.0`
Expected: Build succeeds.

- [ ] **Step 3: Run the existing Redis queue fixture with the default (TaskDelay) implementation to confirm no regression**

Run: `dotnet test source/Halibut.Tests/Halibut.Tests.csproj -f net8.0 --filter "FullyQualifiedName~PendingRequestQueueFixture"`
Expected: PASS.

- [ ] **Step 4: Manually verify the Timer branch works, using the factory test from Task 7 as a template**

Temporarily set `RedisPendingRequestQueueFactory.Implementation = RedisPendingRequestQueueImplementation.Timer;` at the top of a throwaway local test run (e.g. add a one-off `[Test]` that sets it, builds via `RedisPendingRequestQueueBuilder`, and asserts `queue.PendingRequestQueue.GetType() == typeof(TimerBasedRedisPendingRequestQueue)`), run it, then delete it — Task 10 formalizes this properly by parametrizing the whole fixture, so this step is just a sanity check before moving on.

Run: `dotnet test source/Halibut.Tests/Halibut.Tests.csproj -f net8.0 --filter "FullyQualifiedName~<your throwaway test name>"`
Expected: PASS, then delete the throwaway test before committing.

- [ ] **Step 5: Commit**

```bash
git add source/Halibut.Tests/Builders/RedisPendingRequestQueueBuilder.cs
git commit -m "Make RedisPendingRequestQueueBuilder read RedisPendingRequestQueueFactory.Implementation"
```

---

### Task 9: Add a `"Redis-Timer"` case to `AllQueuesTestCasesAttribute`

**Files:**
- Modify: `source/Halibut.Tests/Support/TestAttributes/AllQueuesTestCasesAttribute.cs`

**Interfaces:**
- Consumes: `RedisPendingRequestQueueFactory.Implementation` (Task 7), `RedisPendingRequestQueueBuilder` (Task 8).

This task adds a case to an existing NUnit `TestCaseSource` (`PendingRequestQueueFactories`, a private nested static class inside the attribute — not independently callable from a test, so there's no isolated unit to TDD here). The verification is Step 2 below: run the shared fixture this attribute feeds and confirm a third parametrization shows up and passes.

- [ ] **Step 1: Add the case**

In `source/Halibut.Tests/Support/TestAttributes/AllQueuesTestCasesAttribute.cs`, find:

```csharp
        static class PendingRequestQueueFactories
        {
            public static IEnumerable GetEnumerator()
            {
                var factories = new List<PendingRequestQueueTestCase>();
#if NET8_0_OR_GREATER
                if (EnsureRedisIsAvailableSetupFixture.WillRunRedisTests)
                {
                    
                    factories.Add(new PendingRequestQueueTestCase(PendingRequestQueueTestCase.RedisTestCaseName, () => new RedisPendingRequestQueueBuilder()));
                }
#endif
                factories.Add(new PendingRequestQueueTestCase(PendingRequestQueueTestCase.InMemoryTestCaseName, () => new PendingRequestQueueBuilder()));

                return factories;
            }
        }
    }

    public class PendingRequestQueueTestCase
    {
        
        public static string RedisTestCaseName = "Redis";
        
        public static string InMemoryTestCaseName = "InMemory";
```

Replace with:

```csharp
        static class PendingRequestQueueFactories
        {
            public static IEnumerable GetEnumerator()
            {
                var factories = new List<PendingRequestQueueTestCase>();
#if NET8_0_OR_GREATER
                if (EnsureRedisIsAvailableSetupFixture.WillRunRedisTests)
                {
                    factories.Add(new PendingRequestQueueTestCase(PendingRequestQueueTestCase.RedisTestCaseName, () =>
                    {
                        Halibut.Queue.Redis.RedisPendingRequestQueueFactory.Implementation = Halibut.Queue.Redis.RedisPendingRequestQueueImplementation.TaskDelay;
                        return new RedisPendingRequestQueueBuilder();
                    }));
                    factories.Add(new PendingRequestQueueTestCase(PendingRequestQueueTestCase.RedisTimerTestCaseName, () =>
                    {
                        Halibut.Queue.Redis.RedisPendingRequestQueueFactory.Implementation = Halibut.Queue.Redis.RedisPendingRequestQueueImplementation.Timer;
                        return new RedisPendingRequestQueueBuilder();
                    }));
                }
#endif
                factories.Add(new PendingRequestQueueTestCase(PendingRequestQueueTestCase.InMemoryTestCaseName, () => new PendingRequestQueueBuilder()));

                return factories;
            }
        }
    }

    public class PendingRequestQueueTestCase
    {
        
        public static string RedisTestCaseName = "Redis";

        public static string RedisTimerTestCaseName = "Redis-Timer";
        
        public static string InMemoryTestCaseName = "InMemory";
```

Note: `PendingRequestQueueTestCase.Builder` (see the `Builder` property further down in this file) calls `BuilderBuilder()` — the lambda above — lazily each time a test asks for the builder, which is exactly when we want `RedisPendingRequestQueueFactory.Implementation` set, immediately before `RedisPendingRequestQueueBuilder.Build()` runs and reads it.

- [ ] **Step 2: Build, then run the `[AllQueuesTestCases]`-driven fixture to confirm the new case runs cleanly**

Run: `dotnet build source/Halibut.Tests/Halibut.Tests.csproj -f net8.0`
Expected: Build succeeds.

Run: `dotnet test source/Halibut.Tests/Halibut.Tests.csproj -f net8.0 --filter "FullyQualifiedName~PendingRequestQueueFixture"`
Expected: PASS, and the test output now shows three parametrizations per test (`Redis`, `Redis-Timer`, `InMemory`) instead of two — this is the confirmation that the new case both exists and works, replacing a separate unit test for the `TestCaseSource` itself.

- [ ] **Step 3: Commit**

```bash
git add source/Halibut.Tests/Support/TestAttributes/AllQueuesTestCasesAttribute.cs
git commit -m "Add a Redis-Timer case to AllQueuesTestCasesAttribute"
```

---

### Task 10: Parametrize `RedisPendingRequestQueueFixture` across both implementations

The fixture has 34 call sites doing `new RedisPendingRequestQueue(...)` directly, bypassing `RedisPendingRequestQueueBuilder`. Add a helper that reads the static, replace all 34 call sites with it, and parametrize the whole fixture so every test in it runs against both implementations.

**Files:**
- Modify: `source/Halibut.Tests/Queue/Redis/RedisPendingRequestQueueFixture.cs`

**Interfaces:**
- Consumes: `RedisPendingRequestQueueFactory.Implementation` (Task 7), `TimerBasedRedisPendingRequestQueue` (Task 6), `IRedisPendingRequestQueueTestControls` (Task 2).

- [ ] **Step 1: Add the `[TestFixture]` parametrization (no helper yet)**

In `source/Halibut.Tests/Queue/Redis/RedisPendingRequestQueueFixture.cs`, find:

```csharp
namespace Halibut.Tests.Queue.Redis
{
    [RedisTest]
    public class RedisPendingRequestQueueFixture : BaseTest
    {
        [Test]
        public async Task DequeueAsync_ShouldReturnRequestFromRedis()
```

Replace with:

```csharp
namespace Halibut.Tests.Queue.Redis
{
    [RedisTest]
    [TestFixture(RedisPendingRequestQueueImplementation.TaskDelay)]
    [TestFixture(RedisPendingRequestQueueImplementation.Timer)]
    public class RedisPendingRequestQueueFixture : BaseTest
    {
        readonly RedisPendingRequestQueueImplementation implementation;

        public RedisPendingRequestQueueFixture(RedisPendingRequestQueueImplementation implementation)
        {
            this.implementation = implementation;
        }

        [SetUp]
        public void SelectImplementationUnderTest()
        {
            RedisPendingRequestQueueFactory.Implementation = implementation;
        }

        [Test]
        public async Task DequeueAsync_ShouldReturnRequestFromRedis()
```

No new `using` statements are needed — this file already imports every namespace the parametrization touches (`Halibut.Queue.Redis` for `RedisPendingRequestQueueImplementation`/`RedisPendingRequestQueueFactory`).

- [ ] **Step 2: Bulk-replace all 34 direct construction call sites with `CreateQueue(...)`**

Do this *before* adding the `CreateQueue` helper method itself, so the blanket replace below can't touch the helper's own (legitimate) `new RedisPendingRequestQueue(...)` call — it doesn't exist in the file yet.

Confirm the count first:

```bash
grep -c "new RedisPendingRequestQueue(" source/Halibut.Tests/Queue/Redis/RedisPendingRequestQueueFixture.cs
```

Expected: `34`. Every one of them follows the exact pattern `new RedisPendingRequestQueue(<endpoint>, <watchForRedisLosingAllItsData>, <log>, <transport>, <messageSerialiserAndDataStreamStorage>, <halibutTimeoutsAndLimits>)` with no argument reordering, so a blanket text substitution is safe here:

```bash
sed -i '' 's/new RedisPendingRequestQueue(/CreateQueue(/g' source/Halibut.Tests/Queue/Redis/RedisPendingRequestQueueFixture.cs
```

Confirm it worked:

```bash
grep -c "new RedisPendingRequestQueue(" source/Halibut.Tests/Queue/Redis/RedisPendingRequestQueueFixture.cs
grep -c "CreateQueue(" source/Halibut.Tests/Queue/Redis/RedisPendingRequestQueueFixture.cs
```

Expected: first command prints `0`, second prints `34`.

- [ ] **Step 3: Now add the `CreateQueue` helper method**

Directly below the `[SetUp]` method added in Step 1, insert:

```csharp
        IRedisPendingRequestQueueTestControls CreateQueue(
            Uri endpoint,
            IWatchForRedisLosingAllItsData watchForRedisLosingAllItsData,
            ILog log,
            IHalibutRedisTransport halibutRedisTransport,
            IMessageSerialiserAndDataStreamStorage messageSerialiserAndDataStreamStorage,
            HalibutTimeoutsAndLimits halibutTimeoutsAndLimits)
        {
            return implementation == RedisPendingRequestQueueImplementation.Timer
                ? new TimerBasedRedisPendingRequestQueue(endpoint, watchForRedisLosingAllItsData, log, halibutRedisTransport, messageSerialiserAndDataStreamStorage, halibutTimeoutsAndLimits)
                : new RedisPendingRequestQueue(endpoint, watchForRedisLosingAllItsData, log, halibutRedisTransport, messageSerialiserAndDataStreamStorage, halibutTimeoutsAndLimits);
        }
```

Confirm there's now exactly one `new RedisPendingRequestQueue(` left in the whole file (the one just added, inside `CreateQueue`'s own body):

```bash
grep -c "new RedisPendingRequestQueue(" source/Halibut.Tests/Queue/Redis/RedisPendingRequestQueueFixture.cs
```

Expected: `1`.

- [ ] **Step 4: Build**

Run: `dotnet build source/Halibut.Tests/Halibut.Tests.csproj -f net8.0`
Expected: Build succeeds.

- [ ] **Step 5: Run the full fixture against both implementations**

Run: `dotnet test source/Halibut.Tests/Halibut.Tests.csproj -f net8.0 --filter "FullyQualifiedName~RedisPendingRequestQueueFixture"`
Expected: PASS for every test, twice (once per `[TestFixture]` parametrization — the test output will show each test name suffixed with `(TaskDelay)` and `(Timer)`). This is the authoritative regression check that the Timer-based copy behaves identically to the original across the fixture's full ~30-test suite (heartbeat timeouts, cancellation, unstable connections, data loss, etc.) — this is what actually validates Tasks 4-6, not just the smoke tests added there.

- [ ] **Step 6: Commit**

```bash
git add source/Halibut.Tests/Queue/Redis/RedisPendingRequestQueueFixture.cs
git commit -m "Parametrize RedisPendingRequestQueueFixture across TaskDelay and Timer implementations"
```

---

## Follow-up (not part of this plan)

Once all tasks pass, the switch exists for the perf comparison the user asked for: flip `RedisPendingRequestQueueFactory.Implementation` to `Timer` in a benchmark/load scenario and compare against the `TaskDelay` default. Writing that benchmark is a separate follow-up.
