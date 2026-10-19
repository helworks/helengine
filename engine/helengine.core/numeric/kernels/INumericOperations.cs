namespace helengine {
    /// <summary>Specializes shared geometry and layout formulas without boxing or an ambient numeric policy.</summary>
    public interface INumericOperations<T> {
        /// <summary>Loads a stored engine value under this consumer's storage and arithmetic widths.</summary>
        T Read(Real value);
        /// <summary>Rounds an evaluated value at an engine storage boundary.</summary>
        Real Write(T value);
        /// <summary>Converts a literal or external arithmetic input without a storage boundary.</summary>
        T Number(double value);
        /// <summary>Evaluates addition.</summary>
        T Add(T left, T right);
        /// <summary>Evaluates subtraction.</summary>
        T Subtract(T left, T right);
        /// <summary>Evaluates multiplication.</summary>
        T Multiply(T left, T right);
        /// <summary>Evaluates division.</summary>
        T Divide(T left, T right);
        /// <summary>Evaluates square root at the selected arithmetic width.</summary>
        T Sqrt(T value);
        /// <summary>Returns an absolute value.</summary>
        T Abs(T value);
        /// <summary>Returns the smaller value.</summary>
        T Min(T left, T right);
        /// <summary>Compares two evaluated values.</summary>
        bool LessOrEqual(T left, T right);
        /// <summary>Checks finite classification.</summary>
        bool IsFinite(T value);
        /// <summary>Rounds ties to even and converts to an integer layout dimension.</summary>
        int RoundToInt(T value);
        /// <summary>Rounds a pixel effect according to its selected native-pixel mode.</summary>
        T SnapMagnitude(T value, TextNativePixelMode mode);
    }
}
