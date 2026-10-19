namespace helengine {
    /// <summary>Retains configured arithmetic precision until a result reaches a stored Real destination.</summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public readonly struct RealEvaluation : IEquatable<RealEvaluation>, IComparable<RealEvaluation> {
        /// <summary>Selected arithmetic representation, independent of the stored scalar width.</summary>
        internal readonly RealArithmeticScalar Value;

        /// <summary>Captures an arithmetic result already rounded to the selected evaluation format.</summary>
        /// <param name="value">Selected-width operation result.</param>
        internal RealEvaluation(RealArithmeticScalar value) { Value = value; }

        /// <summary>Expands an evaluated result for explicit fixed-width transport or interop.</summary>
        public double ToDouble() { return (double)Value; }

        /// <summary>Converts fixed primitive input at this RealEvaluation boundary using the selected width.</summary>
        /// <param name="value">Fixed-width input from an authoring literal or external API.</param>
        public static implicit operator RealEvaluation(double value) { return new RealEvaluation((RealArithmeticScalar)value); }

        /// <summary>Converts explicitly to fixed single-precision interop storage.</summary>
        /// <param name="value">Engine numeric value crossing a fixed-width boundary.</param>
        public static explicit operator float(RealEvaluation value) { return (float)value.Value; }

        /// <summary>Converts explicitly to fixed double-precision transport.</summary>
        /// <param name="value">Engine numeric value crossing a fixed-width boundary.</param>
        public static explicit operator double(RealEvaluation value) { return (double)value.Value; }

        /// <summary>Converts explicitly to integral coordinates with truncation.</summary>
        /// <param name="value">Engine numeric coordinate.</param>
        public static explicit operator int(RealEvaluation value) { return (int)value.Value; }

        /// <summary>Formats the selected numeric value instead of its wrapper type name.</summary>
        public override string ToString() { return Value.ToString(); }

        /// <summary>Evaluates a mixed fixed primitive operand at the configured arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static RealEvaluation operator +(RealEvaluation left, double right) { return (RealEvaluation)left + (RealEvaluation)right; }

        /// <summary>Evaluates a mixed fixed primitive operand at the configured arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static RealEvaluation operator +(double left, RealEvaluation right) { return (RealEvaluation)left + (RealEvaluation)right; }

        /// <summary>Evaluates a mixed fixed primitive operand at the configured arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static RealEvaluation operator -(RealEvaluation left, double right) { return (RealEvaluation)left - (RealEvaluation)right; }

        /// <summary>Evaluates a mixed fixed primitive operand at the configured arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static RealEvaluation operator -(double left, RealEvaluation right) { return (RealEvaluation)left - (RealEvaluation)right; }

        /// <summary>Evaluates a mixed fixed primitive operand at the configured arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static RealEvaluation operator *(RealEvaluation left, double right) { return (RealEvaluation)left * (RealEvaluation)right; }

        /// <summary>Evaluates a mixed fixed primitive operand at the configured arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static RealEvaluation operator *(double left, RealEvaluation right) { return (RealEvaluation)left * (RealEvaluation)right; }

        /// <summary>Evaluates a mixed fixed primitive operand at the configured arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static RealEvaluation operator /(RealEvaluation left, double right) { return (RealEvaluation)left / (RealEvaluation)right; }

        /// <summary>Evaluates a mixed fixed primitive operand at the configured arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static RealEvaluation operator /(double left, RealEvaluation right) { return (RealEvaluation)left / (RealEvaluation)right; }

        /// <summary>Evaluates a mixed fixed primitive operand at the configured arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static bool operator ==(RealEvaluation left, double right) { return (RealEvaluation)left == (RealEvaluation)right; }

        /// <summary>Evaluates a mixed fixed primitive operand at the configured arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static bool operator ==(double left, RealEvaluation right) { return (RealEvaluation)left == (RealEvaluation)right; }

        /// <summary>Evaluates a mixed fixed primitive operand at the configured arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static bool operator !=(RealEvaluation left, double right) { return (RealEvaluation)left != (RealEvaluation)right; }

        /// <summary>Evaluates a mixed fixed primitive operand at the configured arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static bool operator !=(double left, RealEvaluation right) { return (RealEvaluation)left != (RealEvaluation)right; }

        /// <summary>Evaluates a mixed fixed primitive operand at the configured arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static bool operator <(RealEvaluation left, double right) { return (RealEvaluation)left < (RealEvaluation)right; }

        /// <summary>Evaluates a mixed fixed primitive operand at the configured arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static bool operator <(double left, RealEvaluation right) { return (RealEvaluation)left < (RealEvaluation)right; }

        /// <summary>Evaluates a mixed fixed primitive operand at the configured arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static bool operator >(RealEvaluation left, double right) { return (RealEvaluation)left > (RealEvaluation)right; }

        /// <summary>Evaluates a mixed fixed primitive operand at the configured arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static bool operator >(double left, RealEvaluation right) { return (RealEvaluation)left > (RealEvaluation)right; }

        /// <summary>Evaluates a mixed fixed primitive operand at the configured arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static bool operator <=(RealEvaluation left, double right) { return (RealEvaluation)left <= (RealEvaluation)right; }

        /// <summary>Evaluates a mixed fixed primitive operand at the configured arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static bool operator <=(double left, RealEvaluation right) { return (RealEvaluation)left <= (RealEvaluation)right; }

        /// <summary>Evaluates a mixed fixed primitive operand at the configured arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static bool operator >=(RealEvaluation left, double right) { return (RealEvaluation)left >= (RealEvaluation)right; }

        /// <summary>Evaluates a mixed fixed primitive operand at the configured arithmetic precision.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static bool operator >=(double left, RealEvaluation right) { return (RealEvaluation)left >= (RealEvaluation)right; }

        /// <summary>Compares selected-width arithmetic results with the IEEE value equality contract.</summary>
        /// <param name="other">Transient numeric result to compare.</param>
        public bool Equals(RealEvaluation other) { return Value.Equals(other.Value); }

        /// <summary>Compares boxed expression results without narrowing them to stored Real.</summary>
        /// <param name="other">Candidate boxed expression result.</param>
        public override bool Equals(object other) { return other is RealEvaluation value && Equals(value); }

        /// <summary>Hashes the selected arithmetic value rather than an implicitly narrowed stored result.</summary>
        public override int GetHashCode() { return Value.GetHashCode(); }

        /// <summary>Adds arithmetic operands and retains the result at configured evaluation precision.</summary>
        /// <param name="left">First evaluation operand.</param>
        /// <param name="right">Second evaluation operand.</param>
        public static RealEvaluation operator +(RealEvaluation left, RealEvaluation right) { return new RealEvaluation(left.Value + right.Value); }

        /// <summary>Subtracts arithmetic operands and retains the result at configured evaluation precision.</summary>
        /// <param name="left">First evaluation operand.</param>
        /// <param name="right">Second evaluation operand.</param>
        public static RealEvaluation operator -(RealEvaluation left, RealEvaluation right) { return new RealEvaluation(left.Value - right.Value); }

        /// <summary>Multiplies arithmetic operands and retains the result at configured evaluation precision.</summary>
        /// <param name="left">First evaluation operand.</param>
        /// <param name="right">Second evaluation operand.</param>
        public static RealEvaluation operator *(RealEvaluation left, RealEvaluation right) { return new RealEvaluation(left.Value * right.Value); }

        /// <summary>Divides arithmetic operands and retains the result at configured evaluation precision.</summary>
        /// <param name="left">First evaluation operand.</param>
        /// <param name="right">Second evaluation operand.</param>
        public static RealEvaluation operator /(RealEvaluation left, RealEvaluation right) { return new RealEvaluation(left.Value / right.Value); }

        /// <summary>Negates a value in the configured evaluation format.</summary>
        /// <param name="value">Operand to negate.</param>
        public static RealEvaluation operator -(RealEvaluation value) { return new RealEvaluation(-value.Value); }

        /// <summary>Tests IEEE equality using the actual selected representation.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static bool operator ==(RealEvaluation left, RealEvaluation right) { return left.Value == right.Value; }

        /// <summary>Tests IEEE inequality using the actual selected representation.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static bool operator !=(RealEvaluation left, RealEvaluation right) { return left.Value != right.Value; }

        /// <summary>Tests ascending order using the actual selected representation.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static bool operator <(RealEvaluation left, RealEvaluation right) { return left.Value < right.Value; }

        /// <summary>Tests descending order using the actual selected representation.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static bool operator >(RealEvaluation left, RealEvaluation right) { return left.Value > right.Value; }

        /// <summary>Tests ascending order or equality using the actual selected representation.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static bool operator <=(RealEvaluation left, RealEvaluation right) { return left.Value <= right.Value; }

        /// <summary>Tests descending order or equality using the actual selected representation.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static bool operator >=(RealEvaluation left, RealEvaluation right) { return left.Value >= right.Value; }

        /// <summary>Adds mixed stored and transient operands without an implicit intermediate store.</summary>
        /// <param name="left">First operand.</param>
        /// <param name="right">Second operand.</param>
        public static RealEvaluation operator +(RealEvaluation left, Real right) { return left + (RealEvaluation)right; }

        /// <summary>Adds mixed stored and transient operands without an implicit intermediate store.</summary>
        /// <param name="left">First operand.</param>
        /// <param name="right">Second operand.</param>
        public static RealEvaluation operator +(Real left, RealEvaluation right) { return (RealEvaluation)left + right; }

        /// <summary>Subtracts mixed stored and transient operands without an implicit intermediate store.</summary>
        /// <param name="left">First operand.</param>
        /// <param name="right">Second operand.</param>
        public static RealEvaluation operator -(RealEvaluation left, Real right) { return left - (RealEvaluation)right; }

        /// <summary>Subtracts mixed stored and transient operands without an implicit intermediate store.</summary>
        /// <param name="left">First operand.</param>
        /// <param name="right">Second operand.</param>
        public static RealEvaluation operator -(Real left, RealEvaluation right) { return (RealEvaluation)left - right; }

        /// <summary>Multiplies mixed stored and transient operands without an implicit intermediate store.</summary>
        /// <param name="left">First operand.</param>
        /// <param name="right">Second operand.</param>
        public static RealEvaluation operator *(RealEvaluation left, Real right) { return left * (RealEvaluation)right; }

        /// <summary>Multiplies mixed stored and transient operands without an implicit intermediate store.</summary>
        /// <param name="left">First operand.</param>
        /// <param name="right">Second operand.</param>
        public static RealEvaluation operator *(Real left, RealEvaluation right) { return (RealEvaluation)left * right; }

        /// <summary>Divides mixed stored and transient operands without an implicit intermediate store.</summary>
        /// <param name="left">First operand.</param>
        /// <param name="right">Second operand.</param>
        public static RealEvaluation operator /(RealEvaluation left, Real right) { return left / (RealEvaluation)right; }

        /// <summary>Divides mixed stored and transient operands without an implicit intermediate store.</summary>
        /// <param name="left">First operand.</param>
        /// <param name="right">Second operand.</param>
        public static RealEvaluation operator /(Real left, RealEvaluation right) { return (RealEvaluation)left / right; }
        /// <summary>Largest finite value in the selected evaluation format.</summary>
        public static RealEvaluation MaxValue { get { return new RealEvaluation(RealArithmeticScalar.MaxValue); } }
        /// <summary>Most negative finite value in the selected evaluation format.</summary>
        public static RealEvaluation MinValue { get { return new RealEvaluation(RealArithmeticScalar.MinValue); } }
        /// <summary>Classifies a selected-width arithmetic value as NaN.</summary>
        /// <param name="value">Input value.</param>
        public static bool IsNaN(RealEvaluation value) { return RealArithmeticScalar.IsNaN(value.Value); }
        /// <summary>Classifies a selected-width arithmetic value as infinite.</summary>
        /// <param name="value">Input value.</param>
        public static bool IsInfinity(RealEvaluation value) { return RealArithmeticScalar.IsInfinity(value.Value); }
        /// <summary>Checks a value without converting it to another precision.</summary>
        /// <param name="value">Input value.</param>
        public static bool IsFinite(RealEvaluation value) { return RealArithmeticScalar.IsFinite(value.Value); }
        /// <summary>Orders expression results using the selected representation.</summary>
        /// <param name="other">Expression to compare.</param>
        public int CompareTo(RealEvaluation other) { return Value.CompareTo(other.Value); }
    }
}
