# Timer-based Redis pending request queue (for perf comparison)

## Goal

Produce a second implementation of the Redis pending request queue that uses
`System.Threading.Timer` instead of the existing `Task.Delay`/`DelayWithoutException`
pattern for its internal waits, so the two can be A/B compared for performance.
Add a static switch so the existing `IPendingRequestQueueFactory` can build either
variant, and make unit tests drive the same switch so both variants get test coverage.

This is explicitly a measurement exercise, not a committed rewrite: .NET's
`Task.Delay` is itself `Timer`-backed internally, so the real difference introduced
here is avoiding the extra `ContinueWith` continuation/allocation used by
`DelayWithoutException`, not "Timer vs no Timer" at the OS level. Keep both
implementations side by side until the comparison is done.

## Scope

Duplicate the classes that are owned by the queue's own request lifecycle and use
the `Task.Delay`/`DelayWithoutException` pattern:

| Existing (unchanged) | New (Timer-based copy) |
|---|---|
| `Halibut/Queue/Redis/RedisPendingRequestQueue.cs` | `TimerBasedRedisPendingRequestQueue` |
| `Halibut/Queue/Redis/NodeHeartBeat/NodeHeartBeatSender.cs` | `TimerBasedNodeHeartBeatSender` |
| `Halibut/Queue/Redis/NodeHeartBeat/NodeHeartBeatWatcher.cs` | `TimerBasedNodeHeartBeatWatcher` |

Each new class is a straight copy of its counterpart, with every
`DelayWithoutException.Delay(...)` call (and the raw `Task.Delay(...)` jittered
backoff call in `DequeueNextAsync`) replaced with a call to a new
`TimerBasedDelay.Delay(...)` helper. No other logic changes.

### Explicitly out of scope

These stay shared and unmodified, used identically by both queue variants:

- `RedisPendingRequest` (one-shot pickup-timeout delay)
- `PollAndSubscribeToResponse`
- `Cancellation/DelayBeforeSubscribingToRequestCancellation`, `Cancellation/WatchForRequestCancellation`
- `NodeHeartBeat/HeartBeatInitialDelay`
- `RedisHelpers/RedisFacade`
- `RedisDataLossDetection/WatchForRedisLosingAllItsData`

These are either one-shot delays or infrastructure shared across the whole Redis
subsystem (connection retries, data-loss detection) — duplicating them would expand
scope well past "the pending request queue" and risks two diverging connection layers.

Also explicitly out of scope: `Halibut.Tests.Support.TestAttributes.PollingQueueTestCase`
and `ClientAndServiceTestCasesBuilder`. That enum drives the full client+server
integration test matrix; adding a third value there would triple that already
resource-intensive suite (see recent commits reducing CI parallelism and marking
fixtures `NonParallelizable`). This work stays scoped to the queue-level unit tests.

## New delay primitive

`Halibut/Util/TimerBasedDelay.cs`:

```csharp
public static class TimerBasedDelay
{
    public static Task Delay(TimeSpan timeSpan, CancellationToken cancellationToken);
}
```

Drop-in replacement for `DelayWithoutException.Delay` — same signature, same
non-throwing-on-cancellation semantics. Implemented with a one-shot
`System.Threading.Timer` + `TaskCompletionSource` instead of
`Task.Delay(...).ContinueWith(...)`:

- Timer fires once after `timeSpan` → completes the `TaskCompletionSource` → disposes the timer.
- A `CancellationTokenRegistration` on `cancellationToken` also completes the
  `TaskCompletionSource` and disposes the timer immediately, without waiting for it to fire.
- Whichever happens first wins; the timer and registration are always disposed exactly once.

## Static switch

`Halibut/Queue/Redis/RedisPendingRequestQueueImplementation.cs`:

```csharp
public enum RedisPendingRequestQueueImplementation
{
    TaskDelay,
    Timer
}
```

`RedisPendingRequestQueueFactory` gets:

```csharp
public static RedisPendingRequestQueueImplementation Implementation { get; set; }
    = RedisPendingRequestQueueImplementation.TaskDelay;
```

`CreateQueue(Uri endpoint)` branches on `Implementation` to construct either
`RedisPendingRequestQueue` or `TimerBasedRedisPendingRequestQueue`, passing the
same arguments to either constructor. Default value means existing behavior is
unchanged unless something explicitly opts in.

`TimerBasedRedisPendingRequestQueue` internally always uses
`TimerBasedNodeHeartBeatSender`/`TimerBasedNodeHeartBeatWatcher` — the switch only
needs to be read once, at the factory boundary.

All new Redis-specific types (`TimerBasedRedisPendingRequestQueue`,
`TimerBasedNodeHeartBeatSender`, `TimerBasedNodeHeartBeatWatcher`,
`RedisPendingRequestQueueImplementation`, and the `Implementation` property on
`RedisPendingRequestQueueFactory`) are wrapped in `#if NET8_0_OR_GREATER`, matching
every existing file under `Halibut/Queue/Redis/`. `TimerBasedDelay` itself is not
gated, matching its ungated sibling `DelayWithoutException`.

## Test wiring

The same static is the single source of truth read by both production code and
tests — no separate test-only flag.

- **`Halibut.Tests/Builders/RedisPendingRequestQueueBuilder.cs`**: currently
  hardcodes `new RedisPendingRequestQueue(...)`. Change it to read
  `RedisPendingRequestQueueFactory.Implementation` and construct the matching
  concrete type.
- **`Halibut.Tests/Support/TestAttributes/AllQueuesTestCasesAttribute.cs`**: add a
  `"Redis-Timer"` case alongside the existing `"Redis"` (kept as-is, implying
  `TaskDelay`) and `"InMemory"` cases. The new case's builder lambda sets the
  static to `Timer` before returning a `RedisPendingRequestQueueBuilder`. This
  automatically extends every test using `[AllQueuesTestCases]` (e.g.
  `PendingRequestQueueFixture`) to also run against the Timer variant.
- **`Halibut.Tests/Queue/Redis/RedisPendingRequestQueueFixture.cs`**: has 34
  call sites doing `new RedisPendingRequestQueue(...)` directly. Add a private
  `CreateQueue(...)` helper that reads the static and returns the right concrete
  type, and replace all 34 call sites with it. Parametrize the fixture with
  `[TestFixture(RedisPendingRequestQueueImplementation.TaskDelay)]` and
  `[TestFixture(RedisPendingRequestQueueImplementation.Timer)]` (constructor takes
  the enum and sets the static for that fixture instance), keeping the existing
  `[RedisTest]` gating so the whole fixture runs against both implementations.

## Risk / assumption called out, not solved

`RedisPendingRequestQueueFactory.Implementation` is a process-global mutable
static. It's safe only if these Redis fixtures don't run concurrently with each
other — which matches the existing precedent in this repo of marking
resource-intensive Redis fixtures `[NonParallelizable]`. No additional
synchronization (locking, thread-static, etc.) is being added around the switch
itself; if flakiness shows up from parallel execution, that's the first place to look.

## Out of scope for this change

- No changes to `HalibutRuntimeBuilder` or any production wiring beyond the
  factory's `CreateQueue` branch — nothing currently constructs
  `RedisPendingRequestQueueFactory` in a way that sets `Implementation` to
  `Timer`, so production behavior is unaffected by default.
- No benchmark harness is included — the switch exists so an existing or
  follow-up benchmark/perf test can flip it and compare. Writing that comparison
  is a follow-up, not part of this change.
