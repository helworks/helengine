namespace helengine;

/// <summary>
/// Identifies a logical action in the portable input system.
/// </summary>
public readonly struct InputActionId : IEquatable<InputActionId> {
    /// <summary>
    /// Creates a new logical action identifier from a stable integer value.
    /// </summary>
    /// <param name="value">Stable action identifier value.</param>
    public InputActionId(int value) {
        Value = value;
    }

    /// <summary>
    /// Gets the stable integer value that represents this action.
    /// </summary>
    public int Value { get; }

    /// <summary>
    /// Determines whether this identifier matches another identifier.
    /// </summary>
    /// <param name="other">Other identifier to compare.</param>
    /// <returns>True when both identifiers carry the same value.</returns>
    public bool Equals(InputActionId other) {
        return Value == other.Value;
    }

    /// <summary>
    /// Determines whether this identifier matches the supplied object.
    /// </summary>
    /// <param name="obj">Object to compare.</param>
    /// <returns>True when the object is an <see cref="InputActionId"/> with the same value.</returns>
    public override bool Equals(object obj) {
        if (obj is InputActionId) {
            return Equals((InputActionId)obj);
        }

        return false;
    }

    /// <summary>
    /// Returns a hash code derived from the identifier value.
    /// </summary>
    /// <returns>Hash code for this identifier.</returns>
    public override int GetHashCode() {
        return Value;
    }

    /// <summary>
    /// Compares two action identifiers for equality.
    /// </summary>
    /// <param name="left">Left identifier.</param>
    /// <param name="right">Right identifier.</param>
    /// <returns>True when both identifiers are equal.</returns>
    public static bool operator ==(InputActionId left, InputActionId right) {
        return left.Equals(right);
    }

    /// <summary>
    /// Compares two action identifiers for inequality.
    /// </summary>
    /// <param name="left">Left identifier.</param>
    /// <param name="right">Right identifier.</param>
    /// <returns>True when the identifiers differ.</returns>
    public static bool operator !=(InputActionId left, InputActionId right) {
        return !left.Equals(right);
    }
}
