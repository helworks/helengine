namespace helengine {
    /// <summary>Retains configured arithmetic precision until a result reaches a stored Real destination.</summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public readonly struct RealEvaluation : IEquatable<RealEvaluation> {
        /// <summary>Selected arithmetic representation, independent of the stored scalar width.</summary>
        internal readonly RealArithmeticScalar Value;

        /// <summary>Captures an arithmetic result already rounded to the selected evaluation format.</summary>
        /// <param name="value">Selected-width operation result.</param>
        internal RealEvaluation(RealArithmeticScalar value) { Value = value; }

        /// <summary>Expands an evaluated result for explicit fixed-width transport or interop.</summary>
        public double ToDouble() { return (double)Value; }

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
    }
}
