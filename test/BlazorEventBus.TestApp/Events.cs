namespace BlazorEventBus.TestApp;

/// <summary>Shared event contracts used across components.</summary>
public sealed record MessageSent(string Text, DateTime Timestamp);
public sealed record CounterChanged(int NewValue);
public sealed record ThemeChanged(string CssClass, string Label);
public sealed record NotificationPosted(string Message, string Level = "info");