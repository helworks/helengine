#pragma once

/// <summary>
/// Represents a lightweight managed event placeholder used by transpiled engine members during native compilation.
/// </summary>
class Event {
public:
    /// <summary>
    /// Initializes an empty event placeholder with no subscriber storage.
    /// </summary>
    Event() = default;

    /// <summary>
    /// Registers a subscriber against the event placeholder.
    /// </summary>
    /// <typeparam name="THandler">Native handler shape provided by the caller.</typeparam>
    /// <param name="handler">Handler instance being attached to the event.</param>
    /// <returns>The current placeholder so chained subscriptions remain compilable.</returns>
    template <typename THandler>
    Event& operator+=(THandler handler) {
        (void)handler;
        return *this;
    }

    /// <summary>
    /// Unregisters a subscriber from the event placeholder.
    /// </summary>
    /// <typeparam name="THandler">Native handler shape provided by the caller.</typeparam>
    /// <param name="handler">Handler instance being detached from the event.</param>
    /// <returns>The current placeholder so chained removals remain compilable.</returns>
    template <typename THandler>
    Event& operator-=(THandler handler) {
        (void)handler;
        return *this;
    }

    /// <summary>
    /// Invokes the event placeholder with arbitrary argument shapes.
    /// </summary>
    /// <typeparam name="TArgs">Native argument shapes forwarded by the transpiled call site.</typeparam>
    /// <param name="args">Arguments supplied by the transpiled call site.</param>
    template <typename... TArgs>
    void Invoke(TArgs... args) {
        Discard(args...);
    }

private:
    /// <summary>
    /// Discards invocation arguments so placeholder calls remain warning-free during compile validation.
    /// </summary>
    /// <typeparam name="TArgs">Argument shapes supplied by the transpiled event invocation.</typeparam>
    /// <param name="args">Arguments that are intentionally ignored by the placeholder runtime.</param>
    template <typename... TArgs>
    static void Discard(TArgs... args) {
        ((void)args, ...);
    }
};
