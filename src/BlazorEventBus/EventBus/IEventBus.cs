namespace BlazorEventBus.EventBus;

/// <summary>
/// Event aggregator for loosely coupled messaging between Blazor components.
/// </summary>
/// <remarks>
/// <para>
/// An <see cref="IEventBus"/> is registered as a scoped service via
/// <c>services.AddBlazorEventBus()</c>. In Blazor Server this matches a single
/// SignalR circuit; in Blazor WebAssembly it matches the application lifetime.
/// </para>
/// <para>
/// Subscriptions returned from the <c>Subscribe</c> overloads are
/// <see cref="IDisposable"/>. Dispose them in your component's <c>Dispose</c>
/// method to stop receiving events. Failing to do so holds a reference to the
/// component for the remainder of the circuit.
/// </para>
/// <para>
/// All members are safe to call concurrently. Handlers are invoked in the
/// order they were subscribed; async handlers are awaited sequentially by
/// <see cref="PublishAsync{TEvent}(TEvent, CancellationToken)"/>.
/// </para>
/// </remarks>
public interface IEventBus
{
    /// <summary>
    /// Subscribes a synchronous handler for events of type <typeparamref name="TEvent"/>.
    /// </summary>
    /// <typeparam name="TEvent">The event type. Typically, a <c>sealed record</c>.</typeparam>
    /// <param name="handler">The callback invoked each time an event of this type is published.</param>
    /// <returns>
    /// A subscription token. Dispose it to unsubscribe. The token is safe to
    /// dispose multiple times.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The bus has been disposed.</exception>
    IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class;

    /// <summary>
    /// Subscribes an asynchronous handler for events of type <typeparamref name="TEvent"/>.
    /// </summary>
    /// <typeparam name="TEvent">The event type. Typically, a <c>sealed record</c>.</typeparam>
    /// <param name="handler">
    /// The async callback invoked each time an event of this type is published.
    /// Receives the event and a <see cref="CancellationToken"/> flowed from the publisher.
    /// </param>
    /// <returns>
    /// A subscription token. Dispose it to unsubscribe. The token is safe to
    /// dispose multiple times.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The bus has been disposed.</exception>
    IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler) where TEvent : class;

    /// <summary>
    /// Publishes an event to all <em>synchronous</em> handlers. Async handlers
    /// registered via <see cref="Subscribe{TEvent}(Func{TEvent, CancellationToken, Task})"/>
    /// are <b>not</b> invoked — use
    /// <see cref="PublishAsync{TEvent}(TEvent, CancellationToken)"/> instead
    /// when any async handlers are registered.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="eventData">The event instance. Must not be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="eventData"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The bus has been disposed.</exception>
    /// <exception cref="AggregateException">One or more handlers threw. Each inner exception is preserved.</exception>
    void Publish<TEvent>(TEvent eventData) where TEvent : class;

    /// <summary>
    /// Publishes an event to all handlers (both synchronous and asynchronous).
    /// Sync handlers execute inline; async handlers are awaited one after another
    /// in subscription order.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="eventData">The event instance. Must not be <see langword="null"/>.</param>
    /// <param name="cancellationToken">
    /// Propagated to every async handler. The loop checks the token between
    /// handlers and cancels the remaining ones if signalled.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="eventData"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The bus has been disposed.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    /// <exception cref="AggregateException">One or more handlers threw. Each inner exception is preserved.</exception>
    Task PublishAsync<TEvent>(TEvent eventData, CancellationToken cancellationToken = default) where TEvent : class;
}