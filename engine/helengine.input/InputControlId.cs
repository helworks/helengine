namespace helengine;

/// <summary>
/// Identifies one physical control on a specific device family and device index.
/// </summary>
public readonly struct InputControlId : IEquatable<InputControlId> {
    /// <summary>
    /// Creates a new control identifier.
    /// </summary>
    /// <param name="deviceKind">Family that owns the control.</param>
    /// <param name="controlKind">Shape of the control value.</param>
    /// <param name="deviceIndex">Zero-based device index within the family.</param>
    /// <param name="controlIndex">Zero-based control index within the device.</param>
    public InputControlId(InputDeviceKind deviceKind, InputControlKind controlKind, int deviceIndex, int controlIndex) {
        DeviceKind = deviceKind;
        ControlKind = controlKind;
        DeviceIndex = deviceIndex;
        ControlIndex = controlIndex;
    }

    /// <summary>
    /// Gets the device family that owns the control.
    /// </summary>
    public InputDeviceKind DeviceKind { get; }

    /// <summary>
    /// Gets the shape of the control value.
    /// </summary>
    public InputControlKind ControlKind { get; }

    /// <summary>
    /// Gets the zero-based device index within the family.
    /// </summary>
    public int DeviceIndex { get; }

    /// <summary>
    /// Gets the zero-based control index within the device.
    /// </summary>
    public int ControlIndex { get; }

    /// <summary>
    /// Determines whether this identifier matches another identifier.
    /// </summary>
    /// <param name="other">Other identifier to compare.</param>
    /// <returns>True when both identifiers carry the same values.</returns>
    public bool Equals(InputControlId other) {
        return DeviceKind == other.DeviceKind &&
               ControlKind == other.ControlKind &&
               DeviceIndex == other.DeviceIndex &&
               ControlIndex == other.ControlIndex;
    }

    /// <summary>
    /// Determines whether this identifier matches the supplied object.
    /// </summary>
    /// <param name="obj">Object to compare.</param>
    /// <returns>True when the object is an <see cref="InputControlId"/> with the same values.</returns>
    public override bool Equals(object obj) {
        if (obj is InputControlId) {
            return Equals((InputControlId)obj);
        }

        return false;
    }

    /// <summary>
    /// Returns a hash code derived from the identifier values.
    /// </summary>
    /// <returns>Hash code for this identifier.</returns>
    public override int GetHashCode() {
        unchecked {
            int hashCode = (int)DeviceKind;
            hashCode = (hashCode * 397) ^ (int)ControlKind;
            hashCode = (hashCode * 397) ^ DeviceIndex;
            hashCode = (hashCode * 397) ^ ControlIndex;
            return hashCode;
        }
    }

    /// <summary>
    /// Compares two control identifiers for equality.
    /// </summary>
    /// <param name="left">Left identifier.</param>
    /// <param name="right">Right identifier.</param>
    /// <returns>True when both identifiers are equal.</returns>
    public static bool operator ==(InputControlId left, InputControlId right) {
        return left.Equals(right);
    }

    /// <summary>
    /// Compares two control identifiers for inequality.
    /// </summary>
    /// <param name="left">Left identifier.</param>
    /// <param name="right">Right identifier.</param>
    /// <returns>True when the identifiers differ.</returns>
    public static bool operator !=(InputControlId left, InputControlId right) {
        return !left.Equals(right);
    }
}
