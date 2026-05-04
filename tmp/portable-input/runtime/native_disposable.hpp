#pragma once

/// <summary>
/// Represents the managed disposable contract expected by transpiled engine types.
/// </summary>
class IDisposable {
public:
    /// <summary>
    /// Releases owned resources exposed by the transpiled managed contract.
    /// </summary>
    virtual ~IDisposable() = default;

    /// <summary>
    /// Releases the resources owned by the implementing instance.
    /// </summary>
    virtual void Dispose() = 0;
};
