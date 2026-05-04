namespace helengine;

/// <summary>
/// Identifies an input context such as gameplay, UI, or an editor mode.
/// </summary>
public readonly struct InputContextId : IEquatable<InputContextId> {
    /// <summary>
    /// Creates a new context identifier from a stable integer value.
    /// </summary>
    /// <param name="value">Stable context identifier value.</param>
    public InputContextId(int value) {
        Value = value;
    }

    /// <summary>
    /// Gets the stable integer value that represents this context.
    /// </summary>
    public int Value { get; }

    /// <summary>
    /// Determines whether this identifier matches another identifier.
    /// </summary>
    /// <param name="other">Other identifier to compare.</param>
    /// <returns>True when both identifiers carry the same value.</returns>
    public bool Equals(InputContextId other) {
        return Value == other.Value;
    }

    /// <summary>
    /// Determines whether this identifier matches the supplied object.
    /// </summary>
    /// <param name="obj">Object to compare.</param>
    /// <returns>True when the object is an <see cref="InputContextId"/> with the same value.</returns>
    public override bool Equals(object obj) {
        if (obj is InputContextId) {
            return Equals((InputContextId)obj);
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
    /// Compares two context identifiers for equality.
    /// </summary>
    /// <param name="left">Left identifier.</param>
    /// <param name="right">Right identifier.</param>
    /// <returns>True when both identifiers are equal.</returns>
    public static bool operator ==(InputContextId left, InputContextId right) {
        return left.Equals(right);
    }

    /// <summary>
    /// Compares two context identifiers for inequality.
    /// </summary>
    /// <param name="left">Left identifier.</param>
    /// <param name="right">Right identifier.</param>
    /// <returns>True when the identifiers differ.</returns>
    public static bool operator !=(InputContextId left, InputContextId right) {
        return !left.Equals(right);
    }
}
