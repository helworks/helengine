#pragma once

/// <summary>
/// Represents a lightweight two-value tuple for transpiled managed tuple flows.
/// </summary>
template <typename... TItems>
class ValueTuple;

/// <summary>
/// Represents a lightweight two-value tuple for transpiled managed tuple flows.
/// </summary>
template <typename T1, typename T2>
class ValueTuple<T1, T2> {
public:
    /// <summary>
    /// Initializes an empty tuple with default-initialized items.
    /// </summary>
    ValueTuple() = default;

    /// <summary>
    /// Initializes the tuple with the supplied item values.
    /// </summary>
    /// <param name="item1">First tuple item.</param>
    /// <param name="item2">Second tuple item.</param>
    ValueTuple(T1 item1, T2 item2)
        : Item1(item1), Item2(item2) {
    }

    /// <summary>
    /// Gets or sets the first tuple item.
    /// </summary>
    T1 Item1;

    /// <summary>
    /// Gets or sets the second tuple item.
    /// </summary>
    T2 Item2;
};

/// <summary>
/// Represents a lightweight three-value tuple for transpiled managed tuple flows.
/// </summary>
template <typename T1, typename T2, typename T3>
class ValueTuple<T1, T2, T3> {
public:
    /// <summary>
    /// Initializes an empty tuple with default-initialized items.
    /// </summary>
    ValueTuple() = default;

    /// <summary>
    /// Initializes the tuple with the supplied item values.
    /// </summary>
    /// <param name="item1">First tuple item.</param>
    /// <param name="item2">Second tuple item.</param>
    /// <param name="item3">Third tuple item.</param>
    ValueTuple(T1 item1, T2 item2, T3 item3)
        : Item1(item1), Item2(item2), Item3(item3) {
    }

    /// <summary>
    /// Gets or sets the first tuple item.
    /// </summary>
    T1 Item1;

    /// <summary>
    /// Gets or sets the second tuple item.
    /// </summary>
    T2 Item2;

    /// <summary>
    /// Gets or sets the third tuple item.
    /// </summary>
    T3 Item3;
};
