namespace BlazorEventBus.Tests;

public sealed record CounterIncremented(int NewValue);
public sealed record UserLoggedIn(string UserId);

public class EventBusTests
{
    [Fact]
    public void Subscribe_SyncHandler_ReceivesPublishedEvent()
    {
        using var bus = new EventBus.EventBus();
        CounterIncremented? received = null;

        using IDisposable _ = bus.Subscribe<CounterIncremented>(e => received = e);
        bus.Publish(new CounterIncremented(42));

        Assert.Equal(new CounterIncremented(42), received);
    }

    [Fact]
    public async Task PublishAsync_InvokesSyncAndAsyncHandlers()
    {
        using var bus = new EventBus.EventBus();
        var hits = new List<string>();

        using IDisposable s1 = bus.Subscribe<CounterIncremented>(e => hits.Add($"sync-{e.NewValue}"));
        using IDisposable s2 = bus.Subscribe<CounterIncremented>(async (e, _) =>
        {
            await Task.Yield();
            hits.Add($"async-{e.NewValue}");
        });

        await bus.PublishAsync(new CounterIncremented(7));

        Assert.Equal(["sync-7", "async-7"], hits);
    }

    [Fact]
    public void Publish_DoesNotInvokeAsyncHandlers()
    {
        using var bus = new EventBus.EventBus();
        var asyncCalled = false;
        var syncCalled = false;

        using IDisposable s1 = bus.Subscribe<CounterIncremented>(_ => syncCalled = true);
        using IDisposable s2 = bus.Subscribe<CounterIncremented>((_, _) =>
        {
            asyncCalled = true;
            return Task.CompletedTask;
        });

        bus.Publish(new CounterIncremented(1));

        Assert.True(syncCalled);
        Assert.False(asyncCalled);
    }

    [Fact]
    public async Task PublishAsync_InvokesHandlersInSubscriptionOrder()
    {
        using var bus = new EventBus.EventBus();
        var order = new List<int>();

        using IDisposable s1 = bus.Subscribe<CounterIncremented>(_ => order.Add(1));
        using IDisposable s2 = bus.Subscribe<CounterIncremented>(async (_, _) =>
        {
            await Task.Yield();
            order.Add(2);
        });
        using IDisposable s3 = bus.Subscribe<CounterIncremented>(_ => order.Add(3));

        await bus.PublishAsync(new CounterIncremented(0));

        Assert.Equal([1, 2, 3], order);
    }

    [Fact]
    public void Dispose_Subscription_StopsReceivingEvents()
    {
        using var bus = new EventBus.EventBus();
        var count = 0;

        IDisposable subscription = bus.Subscribe<CounterIncremented>(_ => count++);
        bus.Publish(new CounterIncremented(1));
        subscription.Dispose();
        bus.Publish(new CounterIncremented(2));

        Assert.Equal(1, count);
    }

    [Fact]
    public void Dispose_Bus_SubsequentSubscribeThrows()
    {
        var bus = new EventBus.EventBus();
        bus.Dispose();

        Assert.Throws<ObjectDisposedException>(() =>
            bus.Subscribe<CounterIncremented>(_ => { }));
    }

    [Fact]
    public void Dispose_Bus_SubsequentPublishThrows()
    {
        var bus = new EventBus.EventBus();
        bus.Dispose();

        Assert.Throws<ObjectDisposedException>(() => bus.Publish(new CounterIncremented(1)));
    }

    [Fact]
    public async Task Dispose_Bus_SubsequentPublishAsyncThrows()
    {
        var bus = new EventBus.EventBus();
        bus.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => bus.PublishAsync(new CounterIncremented(1)));
    }

    [Fact]
    public void Dispose_Bus_IsIdempotent()
    {
        var bus = new EventBus.EventBus();
        bus.Dispose();
        bus.Dispose(); // must not throw

        Assert.Throws<ObjectDisposedException>(() =>
            bus.Subscribe<CounterIncremented>(_ => { }));
    }

    [Fact]
    public void Subscribe_NullHandler_Throws()
    {
        using var bus = new EventBus.EventBus();

        Assert.Throws<ArgumentNullException>(() =>
            bus.Subscribe((Action<CounterIncremented>)null!));
        Assert.Throws<ArgumentNullException>(() =>
            bus.Subscribe<CounterIncremented>(null!));
    }

    [Fact]
    public void Publish_NullEvent_Throws()
    {
        using var bus = new EventBus.EventBus();
        Assert.Throws<ArgumentNullException>(() => bus.Publish<CounterIncremented>(null!));
    }

    [Fact]
    public void Publish_NoSubscribers_DoesNothing()
    {
        using var bus = new EventBus.EventBus();

        Exception? exception = Record.Exception(() => bus.Publish(new CounterIncremented(1)));

        Assert.Null(exception);
    }

    [Fact]
    public void Publish_HandlerThrows_AggregatesAllExceptionsAndRunsAllHandlers()
    {
        using var bus = new EventBus.EventBus();
        var later = false;

        using IDisposable s1 = bus.Subscribe<CounterIncremented>(_ => throw new InvalidOperationException("a"));
        using IDisposable s2 = bus.Subscribe<CounterIncremented>(_ => throw new ArgumentException("b"));
        using IDisposable s3 = bus.Subscribe<CounterIncremented>(_ => later = true);

        var ex = Assert.Throws<AggregateException>(() => bus.Publish(new CounterIncremented(1)));

        Assert.Equal(2, ex.InnerExceptions.Count);
        Assert.Contains(ex.InnerExceptions, e => e is InvalidOperationException);
        Assert.Contains(ex.InnerExceptions, e => e is ArgumentException);
        Assert.True(later, "all handlers must run even if earlier ones throw");
    }

    [Fact]
    public async Task PublishAsync_HandlerThrows_AggregatesAllExceptions()
    {
        using var bus = new EventBus.EventBus();

        using IDisposable s1 = bus.Subscribe<CounterIncremented>(_ => throw new InvalidOperationException("a"));
        using IDisposable s2 = bus.Subscribe<CounterIncremented>(async (_, _) =>
        {
            await Task.Yield();
            throw new ArgumentException("b");
        });

        var ex = await Assert.ThrowsAsync<AggregateException>(
            () => bus.PublishAsync(new CounterIncremented(1)));

        Assert.Equal(2, ex.InnerExceptions.Count);
        Assert.Contains(ex.InnerExceptions, e => e is InvalidOperationException);
        Assert.Contains(ex.InnerExceptions, e => e is ArgumentException);
    }

    [Fact]
    public async Task PublishAsync_CancelledToken_Throws()
    {
        using var bus = new EventBus.EventBus();
        using var cts = new CancellationTokenSource();
        var handlerCalled = false;

        using IDisposable _ = bus.Subscribe<CounterIncremented>(_ => handlerCalled = true);
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => bus.PublishAsync(new CounterIncremented(1), cts.Token));
        Assert.False(handlerCalled);
    }

    [Fact]
    public async Task PublishAsync_FlowsCancellationTokenToHandlers()
    {
        using var bus = new EventBus.EventBus();
        using var cts = new CancellationTokenSource();
        CancellationToken received = CancellationToken.None;

        using IDisposable _ = bus.Subscribe<CounterIncremented>((_, ct) =>
        {
            received = ct;
            return Task.CompletedTask;
        });

        await bus.PublishAsync(new CounterIncremented(1), cts.Token);

        Assert.Equal(cts.Token, received);
    }

    [Fact]
    public void DifferentEventTypes_Are_Independent()
    {
        using var bus = new EventBus.EventBus();
        var counterHits = 0;
        var loginHits = 0;

        using IDisposable s1 = bus.Subscribe<CounterIncremented>(_ => counterHits++);
        using IDisposable s2 = bus.Subscribe<UserLoggedIn>(_ => loginHits++);

        bus.Publish(new CounterIncremented(1));
        bus.Publish(new UserLoggedIn("u"));
        bus.Publish(new CounterIncremented(2));

        Assert.Equal(2, counterHits);
        Assert.Equal(1, loginHits);
    }

    [Fact]
    public async Task Concurrent_SubscribeAndPublish_DoesNotCrashOrLoseSubscribers()
    {
        using var bus = new EventBus.EventBus();
        var hits = 0;

        // Pre-existing subscriber. Subscribers added mid-flight may or may not
        // observe a concurrent publish — the guarantee is only "no crash, no
        // corruption". We assert the pre-existing subscriber sees all events.
        using IDisposable baseline = bus.Subscribe<CounterIncremented>(_ => Interlocked.Increment(ref hits));

        const int publishers = 4;
        const int subscribers = 4;
        const int iterations = 500;

        var tasks = new List<Task>(publishers + subscribers);
        for (var p = 0; p < publishers; p++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (var i = 0; i < iterations; i++)
                {
                    bus.Publish(new CounterIncremented(i));
                }
            }));
        }

        for (var s = 0; s < subscribers; s++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (var i = 0; i < iterations; i++)
                {
                    using IDisposable sub = bus.Subscribe<CounterIncremented>(_ => { });
                }
            }));
        }

        await Task.WhenAll(tasks);

        Assert.Equal(publishers * iterations, hits);
    }

    [Fact]
    public void MultipleSubscriptions_To_Same_Event_All_Fire()
    {
        using var bus = new EventBus.EventBus();
        var a = 0;
        var b = 0;

        using IDisposable s1 = bus.Subscribe<CounterIncremented>(_ => a++);
        using IDisposable s2 = bus.Subscribe<CounterIncremented>(_ => b++);

        bus.Publish(new CounterIncremented(1));
        bus.Publish(new CounterIncremented(2));

        Assert.Equal(2, a);
        Assert.Equal(2, b);
    }

    [Fact]
    public void Unsubscribing_One_Leaves_Others_Active()
    {
        using var bus = new EventBus.EventBus();
        var a = 0;
        var b = 0;

        IDisposable s1 = bus.Subscribe<CounterIncremented>(_ => a++);
        using IDisposable s2 = bus.Subscribe<CounterIncremented>(_ => b++);

        bus.Publish(new CounterIncremented(1));
        s1.Dispose();
        bus.Publish(new CounterIncremented(2));

        Assert.Equal(1, a);
        Assert.Equal(2, b);
    }

    [Fact]
    public void Handler_Can_Unsubscribe_Itself_During_Publish()
    {
        using var bus = new EventBus.EventBus();
        var count = 0;
        IDisposable? self = null;

        self = bus.Subscribe<CounterIncremented>(_ =>
        {
            count++;
            self!.Dispose();
        });

        bus.Publish(new CounterIncremented(1));
        bus.Publish(new CounterIncremented(2)); // should not fire

        Assert.Equal(1, count);
    }

    [Fact]
    public void Handler_Can_Subscribe_New_Handler_During_Publish()
    {
        using var bus = new EventBus.EventBus();
        var hits = new List<string>();
        IDisposable? inner = null;

        IDisposable outer = bus.Subscribe<CounterIncremented>(_ =>
        {
            hits.Add("outer");
            inner ??= bus.Subscribe<CounterIncremented>(_ => hits.Add("inner"));
        });

        try
        {
            bus.Publish(new CounterIncremented(1));
            // Snapshot isolation: inner handler was added mid-publish, should not fire yet.
            Assert.Equal(["outer"], hits);

            hits.Clear();
            bus.Publish(new CounterIncremented(2));
            Assert.Contains("outer", hits);
            Assert.Contains("inner", hits);
        }
        finally
        {
            outer.Dispose();
            inner?.Dispose();
        }
    }

    [Fact]
    public async Task PublishAsync_Handler_Can_Unsubscribe_Itself()
    {
        using var bus = new EventBus.EventBus();
        var count = 0;
        IDisposable? self = null;

        self = bus.Subscribe<CounterIncremented>(async (_, _) =>
        {
            await Task.Yield();
            count++;
            self!.Dispose();
        });

        await bus.PublishAsync(new CounterIncremented(1));
        await bus.PublishAsync(new CounterIncremented(2));

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task PublishAsync_Cancellation_MidFlight_StopsSubsequentHandlers()
    {
        using var bus = new EventBus.EventBus();
        using var cts = new CancellationTokenSource();
        var handlerOrder = new List<string>();

        using IDisposable s1 = bus.Subscribe<CounterIncremented>(async (_, _) =>
        {
            handlerOrder.Add("first-start");
            await cts.CancelAsync();
            handlerOrder.Add("first-end");
        });
        using IDisposable s2 = bus.Subscribe<CounterIncremented>((_, _) =>
        {
            handlerOrder.Add("second");
            return Task.CompletedTask;
        });

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => bus.PublishAsync(new CounterIncremented(1), cts.Token));

        Assert.Contains("first-start", handlerOrder);
        Assert.Contains("first-end", handlerOrder);
        Assert.DoesNotContain("second", handlerOrder);
    }

    [Fact]
    public async Task Concurrent_PublishAsync_DoesNotCorrupt()
    {
        using var bus = new EventBus.EventBus();
        var hits = 0;

        using IDisposable _ = bus.Subscribe<CounterIncremented>(async (_, _) =>
        {
            await Task.Yield();
            Interlocked.Increment(ref hits);
        });

        const int tasks = 8;
        const int iterations = 200;

        await Task.WhenAll(
            Enumerable.Range(0, tasks).Select(_ => Task.Run(async () =>
            {
                for (var i = 0; i < iterations; i++)
                {
                    await bus.PublishAsync(new CounterIncremented(i));
                }
            })));

        Assert.Equal(tasks * iterations, hits);
    }
}