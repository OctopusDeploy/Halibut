using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Tests.Support;
using Halibut.Util.AsyncEx;
using NUnit.Framework;

namespace Halibut.Tests.Util.AsyncEx
{
    [TestFixture]
    public class AsyncLazyFixture
    {
        // Guards against a broken implementation deadlocking and hanging the whole test run.
        static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(30);

        [Test]
        public async Task GettingTheValueTwiceSequentially_CallsTheFactoryOnce()
        {
            var callCount = 0;
            var lazy = new AsyncLazy<string>(_ =>
            {
                Interlocked.Increment(ref callCount);
                return Task.FromResult($"value{callCount}");
            });

            var first = await lazy.Value(CancellationToken.None);
            var second = await lazy.Value(CancellationToken.None);

            first.Should().Be("value1");
            second.Should().Be("value1");
            callCount.Should().Be(1);
        }

        [Test]
        public async Task OverlappingCallers_BothReceiveTheValue_AndTheFactoryIsCalledOnce()
        {
            var callCount = 0;
            var factoryEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseFactory = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var lazy = new AsyncLazy<string>(async _ =>
            {
                Interlocked.Increment(ref callCount);
                factoryEntered.TrySetResult(true);
                await releaseFactory.Task;
                return $"value{callCount}";
            });

            var firstCaller = lazy.Value(CancellationToken.None);
            await WithTimeout(factoryEntered.Task);
            var secondCaller = lazy.Value(CancellationToken.None);

            firstCaller.IsCompleted.Should().BeFalse("the factory is stalled");
            secondCaller.IsCompleted.Should().BeFalse("the second caller should be waiting on the first caller's factory call");

            releaseFactory.SetResult(true);
            var results = await WithTimeout(Task.WhenAll(firstCaller, secondCaller));

            results.Should().AllBe("value1");
            callCount.Should().Be(1);
        }

        [Test]
        public async Task WhenTheFactoryThrows_TheFailureIsNotCached_AndTheFactoryIsCalledAgainOnTheNextRequest()
        {
            var callCount = 0;
            var lazy = new AsyncLazy<string>(async _ =>
            {
                var call = Interlocked.Increment(ref callCount);
                await Task.Yield();
                if (call == 1) throw new InvalidOperationException("first call fails");
                return $"value{callCount}";
            });

            await AssertException.Throws<InvalidOperationException>(lazy.Value(CancellationToken.None));
            lazy.HasValue.Should().BeFalse();

            var value = await WithTimeout(lazy.Value(CancellationToken.None));

            value.Should().Be($"value2");
            callCount.Should().Be(2);
        }

        [Test]
        public async Task CanCacheNull_ForANullableReferenceType() // Note that a nullable value type runs the same code, we don't need a separate test.
        {
            var callCount = 0;
            var lazy = new AsyncLazy<string?>(_ =>
            {
                Interlocked.Increment(ref callCount);
                return Task.FromResult<string?>(null);
            });

            (await lazy.Value(CancellationToken.None)).Should().BeNull();
            (await lazy.Value(CancellationToken.None)).Should().BeNull();

            lazy.HasValue.Should().BeTrue();
            callCount.Should().Be(1);
        }

        [Test]
        public async Task HasValue_IsOnlyTrueOnceTheFactoryHasCompleted()
        {
            var releaseFactory = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var lazy = new AsyncLazy<string>(async _ =>
            {
                await releaseFactory.Task;
                return "value";
            });

            lazy.HasValue.Should().BeFalse("nothing has requested the value yet");

            var caller = lazy.Value(CancellationToken.None);
            lazy.HasValue.Should().BeFalse("the factory is still running");

            releaseFactory.SetResult(true);
            await WithTimeout(caller);

            lazy.HasValue.Should().BeTrue();
        }

        [Test]
        public async Task WhenTheFactoryThrowsWhileAnotherCallerIsWaiting_TheWaitingCallerCallsTheFactoryItself()
        {
            var callCount = 0;
            var releaseFirstFactoryCall = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var lazy = new AsyncLazy<string>(async _ =>
            {
                var call = Interlocked.Increment(ref callCount);
                if (call == 1)
                {
                    await releaseFirstFactoryCall.Task;
                    throw new InvalidOperationException("first call fails");
                }

                return $"value{callCount}";
            });

            var firstCaller = lazy.Value(CancellationToken.None);
            var secondCaller = lazy.Value(CancellationToken.None);

            releaseFirstFactoryCall.SetResult(true);

            await AssertException.Throws<InvalidOperationException>(WithTimeout(firstCaller));
            (await WithTimeout(secondCaller)).Should().Be("value2", "the second caller should not observe the first caller's failure");
            callCount.Should().Be(2);
        }

        [Test]
        public async Task TheCallersCancellationTokenIsPassedToTheFactory()
        {
            using var cts = new CancellationTokenSource();
            CancellationToken? tokenSeenByFactory = null;
            var lazy = new AsyncLazy<string>(ct =>
            {
                tokenSeenByFactory = ct;
                return Task.FromResult("value");
            });

            await lazy.Value(cts.Token);

            tokenSeenByFactory.Should().Be(cts.Token);
        }

        [Test]
        public async Task WhenTheTokenIsAlreadyCancelled_AndThereIsNoValue_ThrowsWithoutCallingTheFactory()
        {
            var callCount = 0;
            var lazy = new AsyncLazy<string>(_ =>
            {
                Interlocked.Increment(ref callCount);
                return Task.FromResult($"value{callCount}");
            });
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            await AssertException.Throws<OperationCanceledException>(lazy.Value(cts.Token));

            callCount.Should().Be(0);
            lazy.HasValue.Should().BeFalse();
            (await lazy.Value(CancellationToken.None)).Should().Be("value1");
        }

        [Test]
        public async Task CancellingACallerWaitingOnAnotherCallersFactoryCall_OnlyCancelsThatCaller()
        {
            var callCount = 0;
            var releaseFactory = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var lazy = new AsyncLazy<string>(async _ =>
            {
                Interlocked.Increment(ref callCount);
                await releaseFactory.Task;
                return $"value{callCount}";
            });

            var firstCaller = lazy.Value(CancellationToken.None);
            using var cts = new CancellationTokenSource();
            var waitingCaller = lazy.Value(cts.Token);

            await cts.CancelAsync();

            await AssertException.Throws<OperationCanceledException>(WithTimeout(waitingCaller));
            firstCaller.IsCompleted.Should().BeFalse("cancelling a waiting caller should not affect the in-progress factory call");

            releaseFactory.SetResult(true);

            (await WithTimeout(firstCaller)).Should().Be("value1");
            callCount.Should().Be(1);
        }

        [Test]
        public async Task WhenTheCallerRunningTheFactoryCancels_TheFactoryIsCancelled_AndTheWaitingCallerRunsTheFactoryAgain()
        {
            var callCount = 0;
            var firstFactoryCallWasCancelled = false;
            var lazy = new AsyncLazy<string>(async ct =>
            {
                var call = Interlocked.Increment(ref callCount);
                if (call == 1)
                {
                    try
                    {
                        await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        firstFactoryCallWasCancelled = true;
                        throw;
                    }
                }

                return $"value{call}";
            });

            using var firstCallerCts = new CancellationTokenSource();
            var firstCaller = lazy.Value(firstCallerCts.Token);
            var secondCaller = lazy.Value(CancellationToken.None);

            secondCaller.IsCompleted.Should().BeFalse("the second caller should be waiting on the first caller's factory call");

            await firstCallerCts.CancelAsync();

            await AssertException.Throws<OperationCanceledException>(WithTimeout(firstCaller));
            firstFactoryCallWasCancelled.Should().BeTrue("the first caller's token should flow into the factory and cancel it");

            (await WithTimeout(secondCaller)).Should().Be("value2", "the second caller should run the factory again under its own token");
            callCount.Should().Be(2);
        }

        static async Task<T> WithTimeout<T>(Task<T> task)
        {
            await ((Task)task).TimeoutAfter(TestTimeout, CancellationToken.None);
            return await task;
        }
    }
}
