namespace helengine {
    /// <summary>Stores one engine numeric value at the build-selected IEEE width.</summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public readonly struct Real : IEquatable<Real>, IComparable<Real> {
        /// <summary>Actual selected storage; evaluation uses a separate transient representation.</summary>
        readonly RealStorageScalar Value;

        /// <summary>Stores an already converted scalar without introducing a wider intermediary.</summary>
        /// <param name="value">Value already rounded to the selected storage format.</param>
        Real(RealStorageScalar value) { Value = value; }

        /// <summary>Rounds an explicit fixed double input to the selected storage format.</summary>
        /// <param name="value">Fixed-width input crossing into configurable engine storage.</param>
        /// <returns>The value rounded at this stored destination.</returns>
        public static Real FromDouble(double value) {
            if (RealRepresentation.StorageBits == 16) {
                return new Real((RealStorageScalar)BitConverter.UInt16BitsToHalf(Binary16Codec.Encode(value)));
            }
            return new Real((RealStorageScalar)value);
        }

        /// <summary>Expands the stored value to an explicitly fixed double transport value.</summary>
        public double ToDouble() { return (double)Value; }

        /// <summary>Converts to a fixed single-precision external interface explicitly.</summary>
        public float ToSingle() { return (float)Value; }

        /// <summary>Loads storage into the selected arithmetic representation before evaluation.</summary>
        /// <param name="value">Stored engine scalar.</param>
        public static implicit operator RealEvaluation(Real value) { return new RealEvaluation((RealArithmeticScalar)value.Value); }

        /// <summary>Rounds an evaluated expression exactly when it reaches a stored destination.</summary>
        /// <param name="value">Transient expression result.</param>
        public static implicit operator Real(RealEvaluation value) { return new Real((RealStorageScalar)value.Value); }

        /// <summary>Converts fixed primitive input at this Real boundary using the selected width.</summary>
        /// <param name="value">Fixed-width input from an authoring literal or external API.</param>
        public static implicit operator Real(double value) { return FromDouble(value); }

        /// <summary>Converts explicitly to fixed single-precision interop storage.</summary>
        /// <param name="value">Engine numeric value crossing a fixed-width boundary.</param>
        public static explicit operator float(Real value) { return (float)value.Value; }

        /// <summary>Converts explicitly to fixed double-precision transport.</summary>
        /// <param name="value">Engine numeric value crossing a fixed-width boundary.</param>
        public static explicit operator double(Real value) { return (double)value.Value; }

        /// <summary>Converts explicitly to integral coordinates with truncation.</summary>
        /// <param name="value">Engine numeric coordinate.</param>
        public static explicit operator int(Real value) { return (int)value.Value; }

        /// <summary>Formats the selected numeric value instead of its wrapper type name.</summary>
        public override string ToString() { return Value.ToString(); }

        /// <summary>Evaluates a mixed fixed primitive operand at the configured arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static RealEvaluation operator +(Real left, double right) { return (RealEvaluation)left + (RealEvaluation)right; }

        /// <summary>Evaluates a mixed fixed primitive operand at the configured arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static RealEvaluation operator +(double left, Real right) { return (RealEvaluation)left + (RealEvaluation)right; }

        /// <summary>Evaluates a mixed fixed primitive operand at the configured arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static RealEvaluation operator -(Real left, double right) { return (RealEvaluation)left - (RealEvaluation)right; }

        /// <summary>Evaluates a mixed fixed primitive operand at the configured arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static RealEvaluation operator -(double left, Real right) { return (RealEvaluation)left - (RealEvaluation)right; }

        /// <summary>Evaluates a mixed fixed primitive operand at the configured arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static RealEvaluation operator *(Real left, double right) { return (RealEvaluation)left * (RealEvaluation)right; }

        /// <summary>Evaluates a mixed fixed primitive operand at the configured arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static RealEvaluation operator *(double left, Real right) { return (RealEvaluation)left * (RealEvaluation)right; }

        /// <summary>Evaluates a mixed fixed primitive operand at the configured arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static RealEvaluation operator /(Real left, double right) { return (RealEvaluation)left / (RealEvaluation)right; }

        /// <summary>Evaluates a mixed fixed primitive operand at the configured arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static RealEvaluation operator /(double left, Real right) { return (RealEvaluation)left / (RealEvaluation)right; }

        /// <summary>Evaluates a mixed fixed primitive operand at the configured arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static bool operator ==(Real left, double right) { return (RealEvaluation)left == (RealEvaluation)right; }

        /// <summary>Evaluates a mixed fixed primitive operand at the configured arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static bool operator ==(double left, Real right) { return (RealEvaluation)left == (RealEvaluation)right; }

        /// <summary>Evaluates a mixed fixed primitive operand at the configured arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static bool operator !=(Real left, double right) { return (RealEvaluation)left != (RealEvaluation)right; }

        /// <summary>Evaluates a mixed fixed primitive operand at the configured arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static bool operator !=(double left, Real right) { return (RealEvaluation)left != (RealEvaluation)right; }

        /// <summary>Evaluates a mixed fixed primitive operand at the configured arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static bool operator <(Real left, double right) { return (RealEvaluation)left < (RealEvaluation)right; }

        /// <summary>Evaluates a mixed fixed primitive operand at the configured arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static bool operator <(double left, Real right) { return (RealEvaluation)left < (RealEvaluation)right; }

        /// <summary>Evaluates a mixed fixed primitive operand at the configured arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static bool operator >(Real left, double right) { return (RealEvaluation)left > (RealEvaluation)right; }

        /// <summary>Evaluates a mixed fixed primitive operand at the configured arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static bool operator >(double left, Real right) { return (RealEvaluation)left > (RealEvaluation)right; }

        /// <summary>Evaluates a mixed fixed primitive operand at the configured arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static bool operator <=(Real left, double right) { return (RealEvaluation)left <= (RealEvaluation)right; }

        /// <summary>Evaluates a mixed fixed primitive operand at the configured arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static bool operator <=(double left, Real right) { return (RealEvaluation)left <= (RealEvaluation)right; }

        /// <summary>Evaluates a mixed fixed primitive operand at the configured arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static bool operator >=(Real left, double right) { return (RealEvaluation)left >= (RealEvaluation)right; }

        /// <summary>Evaluates a mixed fixed primitive operand at the configured arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static bool operator >=(double left, Real right) { return (RealEvaluation)left >= (RealEvaluation)right; }

        /// <summary>Compares stored numeric values with the underlying IEEE value equality contract.</summary>
        /// <param name="other">Stored value to compare.</param>
        public bool Equals(Real other) { return Value.Equals(other.Value); }

        /// <summary>Compares boxed engine scalars without converting to an unrelated primitive type.</summary>
        /// <param name="other">Candidate boxed scalar.</param>
        public override bool Equals(object other) { return other is Real value && Equals(value); }

        /// <summary>Hashes the actual stored IEEE value using its selected representation.</summary>
        public override int GetHashCode() { return Value.GetHashCode(); }

        /// <summary>Adds stored operands in the selected evaluation format without storing an intermediate.</summary>
        /// <param name="left">First stored operand.</param>
        /// <param name="right">Second stored operand.</param>
        public static RealEvaluation operator +(Real left, Real right) { return (RealEvaluation)left + (RealEvaluation)right; }

        /// <summary>Subtracts stored operands in the selected evaluation format without storing an intermediate.</summary>
        /// <param name="left">First stored operand.</param>
        /// <param name="right">Second stored operand.</param>
        public static RealEvaluation operator -(Real left, Real right) { return (RealEvaluation)left - (RealEvaluation)right; }

        /// <summary>Multiplies stored operands in the selected evaluation format without storing an intermediate.</summary>
        /// <param name="left">First stored operand.</param>
        /// <param name="right">Second stored operand.</param>
        public static RealEvaluation operator *(Real left, Real right) { return (RealEvaluation)left * (RealEvaluation)right; }

        /// <summary>Divides stored operands in the selected evaluation format without storing an intermediate.</summary>
        /// <param name="left">First stored operand.</param>
        /// <param name="right">Second stored operand.</param>
        public static RealEvaluation operator /(Real left, Real right) { return (RealEvaluation)left / (RealEvaluation)right; }

        /// <summary>Negates a value in the configured evaluation format.</summary>
        /// <param name="value">Operand to negate.</param>
        public static RealEvaluation operator -(Real value) { return -((RealEvaluation)value); }

        /// <summary>Tests IEEE equality using the actual selected representation.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static bool operator ==(Real left, Real right) { return left.Value == right.Value; }

        /// <summary>Tests IEEE inequality using the actual selected representation.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static bool operator !=(Real left, Real right) { return left.Value != right.Value; }

        /// <summary>Tests ascending order using the actual selected representation.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static bool operator <(Real left, Real right) { return left.Value < right.Value; }

        /// <summary>Tests descending order using the actual selected representation.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static bool operator >(Real left, Real right) { return left.Value > right.Value; }

        /// <summary>Tests ascending order or equality using the actual selected representation.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static bool operator <=(Real left, Real right) { return left.Value <= right.Value; }

        /// <summary>Tests descending order or equality using the actual selected representation.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static bool operator >=(Real left, Real right) { return left.Value >= right.Value; }
        /// <summary>Orders stored values using the selected IEEE representation, including NaN ordering.</summary>
        /// <param name="other">Stored value to compare.</param>
        public int CompareTo(Real other) { return Value.CompareTo(other.Value); }

        /// <summary>Compares a stored value and an expression at arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static bool operator ==(Real left, RealEvaluation right) { return (RealEvaluation)left == (RealEvaluation)right; }

        /// <summary>Compares a stored value and an expression at arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static bool operator ==(RealEvaluation left, Real right) { return (RealEvaluation)left == (RealEvaluation)right; }

        /// <summary>Compares a stored value and an expression at arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static bool operator !=(Real left, RealEvaluation right) { return (RealEvaluation)left != (RealEvaluation)right; }

        /// <summary>Compares a stored value and an expression at arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static bool operator !=(RealEvaluation left, Real right) { return (RealEvaluation)left != (RealEvaluation)right; }

        /// <summary>Compares a stored value and an expression at arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static bool operator <(Real left, RealEvaluation right) { return (RealEvaluation)left < (RealEvaluation)right; }

        /// <summary>Compares a stored value and an expression at arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static bool operator <(RealEvaluation left, Real right) { return (RealEvaluation)left < (RealEvaluation)right; }

        /// <summary>Compares a stored value and an expression at arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static bool operator >(Real left, RealEvaluation right) { return (RealEvaluation)left > (RealEvaluation)right; }

        /// <summary>Compares a stored value and an expression at arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static bool operator >(RealEvaluation left, Real right) { return (RealEvaluation)left > (RealEvaluation)right; }

        /// <summary>Compares a stored value and an expression at arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static bool operator <=(Real left, RealEvaluation right) { return (RealEvaluation)left <= (RealEvaluation)right; }

        /// <summary>Compares a stored value and an expression at arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static bool operator <=(RealEvaluation left, Real right) { return (RealEvaluation)left <= (RealEvaluation)right; }

        /// <summary>Compares a stored value and an expression at arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static bool operator >=(Real left, RealEvaluation right) { return (RealEvaluation)left >= (RealEvaluation)right; }

        /// <summary>Compares a stored value and an expression at arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static bool operator >=(RealEvaluation left, Real right) { return (RealEvaluation)left >= (RealEvaluation)right; }

    }
}
