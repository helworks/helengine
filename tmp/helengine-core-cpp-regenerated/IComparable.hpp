#pragma once

/// <summary>
/// Minimal native placeholder for the managed IComparable contract.
/// </summary>
class IComparable {
public:
    /// <summary>
    /// Releases the placeholder contract through a polymorphic base destructor.
    /// </summary>
    virtual ~IComparable() = default;
};
