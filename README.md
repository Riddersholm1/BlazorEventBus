# BlazorEventBus

A lightweight, **circuit-scoped** event aggregator for Blazor. Enables loosely
coupled messaging between components without them having to know about each
other.

- Per Blazor circuit (scoped DI lifetime)
- Synchronous **and** asynchronous handlers (single async-first publish API)
- Thread-safe under concurrent publish/subscribe
- Deterministic disposal — dispose a subscription, stop receiving events
- Zero runtime dependencies beyond `Microsoft.Extensions.DependencyInjection.Abstractions`
- .NET 10 / Blazor 10
- MIT licensed

## Installation

```bash
dotnet add package BlazorEventBus
```

## Register the bus

```csharp
// Program.cs
using BlazorEventBus;

builder.Services.AddBlazorEventBus();
```

That's it. `IEventBus` is now resolvable in any component and is scoped per
Blazor Server circuit (or per application in Blazor WebAssembly).

## Define an event

Events are plain types — the recommended form is a `sealed record` so they're
immutable, value-compared, and cheap to create:

```csharp
public sealed record CounterIncremented(int NewValue);
public sealed record UserLoggedIn(string UserId, DateTimeOffset At);
```

## Publish

Publishing is always asynchronous. `PublishAsync` invokes both sync and async
handlers — sync handlers execute inline, async handlers are awaited one after
another in subscription order.

```csharp
@inject IEventBus EventBus

private int _count;

private async Task Increment()
{
    _count++;
    await EventBus.PublishAsync(new CounterIncremented(_count));
}
```

## Subscribe

Subscriptions are `IDisposable`. Keep the token and dispose it when your
component is disposed.

### Sync handler

```csharp
@implements IDisposable
@inject IEventBus EventBus

private IDisposable? _subscription;
private int _value;

protected override void OnInitialized()
{
    _subscription = EventBus.Subscribe<CounterIncremented>(OnCounterIncremented);
}

private void OnCounterIncremented(CounterIncremented e)
{
    _value = e.NewValue;
    InvokeAsync(StateHasChanged);
}

public void Dispose() => _subscription?.Dispose();
```

### Async handler

```csharp
protected override void OnInitialized()
{
    _subscription = EventBus.Subscribe<UserLoggedIn>(async (e, ct) =>
    {
        await _audit.RecordAsync(e, ct);
        await InvokeAsync(StateHasChanged);
    });
}
```

The cancellation token comes from the publisher's `PublishAsync` call.

## Full component example

```razor
@page "/counter-display"
@implements IDisposable
@inject IEventBus EventBus

<p>Latest count: @_value</p>

@code
{
    private IDisposable? _subscription;
    private int _value;

    protected override void OnInitialized()
    {
        _subscription = EventBus.Subscribe<CounterIncremented>(e =>
        {
            _value = e.NewValue;
            InvokeAsync(StateHasChanged);
        });
    }

    public void Dispose() => _subscription?.Dispose();
}
```

Now any other component in the same circuit can
`PublishAsync(new CounterIncremented(...))` and this display will update —
with no direct reference between them.

## Publish semantics

`PublishAsync` is the single publish entry point.

| Handler kind | Behavior                                   |
| ------------ | ------------------------------------------ |
| Sync         | Invoked inline on the publishing thread    |
| Async        | Awaited sequentially, in subscription order |

Handlers run **sequentially** within a single publish for deterministic
ordering. If a handler throws, every other handler still runs; the exceptions
are aggregated into an `AggregateException`.

## Disposal & lifetime

- **Subscription**: `IDisposable`. Dispose it to stop receiving events. Safe
  to dispose multiple times.
- **EventBus**: registered as **scoped**; disposed automatically by the DI
  container when the Blazor circuit (Server) or application (WebAssembly)
  ends. After disposal, `Subscribe` and `PublishAsync` throw
  `ObjectDisposedException`.

**Always dispose your subscriptions** — otherwise the bus holds a strong
reference to your component for the lifetime of the circuit.

## Concurrency

The bus is safe for concurrent use:

- Subscriptions are stored in a `ConcurrentDictionary<Type, ImmutableList<…>>`.
- `PublishAsync` takes a lock-free snapshot before iterating, so handlers that
  subscribe/unsubscribe during publication don't corrupt the iteration.
- Subscribers added *during* a publish will see subsequent events but may not
  see the publish already in flight — the usual event-aggregator guarantee.

## Cancellation

`PublishAsync` accepts an optional `CancellationToken`:

```csharp
await EventBus.PublishAsync(new UserLoggedIn(id, DateTimeOffset.UtcNow), ct);
```

The token is checked before each handler and flowed to every async handler.

## API at a glance

```csharp
public interface IEventBus
{
    IDisposable Subscribe<TEvent>(Action<TEvent> handler)
        where TEvent : notnull;

    IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : notnull;

    Task PublishAsync<TEvent>(TEvent eventData, CancellationToken cancellationToken = default)
        where TEvent : notnull;
}
```

```csharp
public static class BlazorEventBusServiceCollectionExtensions
{
    public static IServiceCollection AddBlazorEventBus(this IServiceCollection services);
}
```

## Migrating from 1.x

Version 2.0 removes the synchronous `Publish<TEvent>` overload in favour of a
single async-first publish API. Blazor is async end-to-end anyway (lifecycle
methods, `EventCallback`, `StateHasChanged` via `InvokeAsync`), and keeping
one publish method removes the footgun where a sync `Publish` would throw if
any async handler happened to be registered.

## FAQ

**Why scoped and not singleton?**
A singleton would leak events between users/circuits. Scoped gives each
circuit its own bus, which matches how Blazor components expect to live.

**Does it support `ValueTask`?**
Not currently. Handlers return `Task`. If you have a hot-path use case,
open an issue.

**Does it support weak references to avoid leaks from forgotten subscribers?**
No, it uses strong references — matching the standard event-aggregator
pattern. Always dispose your subscriptions in `Dispose()`.

**Can I subscribe to a base type and receive derived events?**
No, subscription is type-exact. Subscribing to `BaseEvent` does not pick up
`DerivedEvent : BaseEvent`. This keeps the routing fast and the semantics
obvious.

## License

[MIT](LICENSE) © 2026 Jesper Bruhn Riddersholm