using System.Collections.Concurrent;
using RomCleanup.Contracts.Models;

namespace RomCleanup.Infrastructure.Events;

/// <summary>
/// Lightweight in-process pub/sub event bus.
/// Mirrors EventBus.ps1 with topic-based routing and wildcard matching.
/// Thread-safe via lock on all mutation/read operations.
/// </summary>
public sealed class EventBus
{
    private readonly Dictionary<string, List<EventSubscription>> _subscriptions = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();
    private int _sequence;

    /// <summary>
    /// Resets the event bus (new session).
    /// </summary>
    public void Initialize()
    {
        lock (_lock)
        {
            _subscriptions.Clear();
            _sequence = 0;
        }
    }

    /// <summary>
    /// Subscribes a handler to a topic. Returns subscription ID.
    /// </summary>
    public string Subscribe(string topic, Action<EventPayload> handler)
    {
        lock (_lock)
        {
            var id = $"sub-{Interlocked.Increment(ref _sequence)}";
            var subscription = new EventSubscription { Id = id, Topic = topic, Handler = handler };

            if (!_subscriptions.TryGetValue(topic, out var list))
            {
                list = new List<EventSubscription>();
                _subscriptions[topic] = list;
            }
            list.Add(subscription);

            return id;
        }
    }

    /// <summary>
    /// Unsubscribes a handler by subscription ID.
    /// </summary>
    public bool Unsubscribe(string subscriptionId)
    {
        lock (_lock)
        {
            foreach (var (_, list) in _subscriptions)
            {
                var idx = list.FindIndex(s => s.Id == subscriptionId);
                if (idx >= 0)
                {
                    list.RemoveAt(idx);
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Publishes an event to all matching subscribers (exact + wildcard).
    /// Continues to remaining subscribers even if one throws.
    /// </summary>
    public int Publish(string topic, object? data = null)
    {
        var payload = new EventPayload
        {
            Topic = topic,
            Data = data,
            Timestamp = DateTime.UtcNow.ToString("o")
        };

        var handlers = CollectHandlers(topic);
        int delivered = 0;

        foreach (var handler in handlers)
        {
            try
            {
                handler(payload);
                delivered++;
            }
            catch
            {
                // Continue to remaining subscribers (mirrors PS behavior)
            }
        }

        return delivered;
    }

    /// <summary>
    /// Gets the current subscription count.
    /// </summary>
    public int SubscriptionCount
    {
        get
        {
            lock (_lock)
            {
                return _subscriptions.Values.Sum(l => l.Count);
            }
        }
    }

    private List<Action<EventPayload>> CollectHandlers(string topic)
    {
        lock (_lock)
        {
            var handlers = new List<Action<EventPayload>>();

            // Exact matches
            if (_subscriptions.TryGetValue(topic, out var exact))
                handlers.AddRange(exact.Select(s => s.Handler));

            // Wildcard matches (e.g. "AppState.*" matches "AppState.Changed")
            // Also handles bare "*" to match all topics
            foreach (var (pattern, subs) in _subscriptions)
            {
                if (string.Equals(pattern, topic, StringComparison.OrdinalIgnoreCase))
                    continue; // already handled by exact match
                if (IsWildcardMatch(pattern, topic))
                    handlers.AddRange(subs.Select(s => s.Handler));
            }

            return handlers;
        }
    }

    private static bool IsWildcardMatch(string pattern, string topic)
    {
        if (!pattern.Contains('*')) return false;

        // Bare "*" matches all topics
        if (pattern == "*") return true;

        // "AppState.*" → prefix "AppState." matches "AppState.Changed"
        var prefix = pattern.Replace(".*", ".");
        if (prefix.EndsWith('.'))
            return topic.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

        // Generic wildcard: "scan*" matches "scan-complete"
        var basePattern = pattern.Replace("*", "");
        return topic.StartsWith(basePattern, StringComparison.OrdinalIgnoreCase);
    }
}
