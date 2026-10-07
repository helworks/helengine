namespace helengine.media;
/// <summary>An exact normalized rational number of seconds or frames per second.</summary>
public readonly struct MediaTime : IComparable<MediaTime>, IEquatable<MediaTime> {
    /// <summary>Constructs a normalized value with a strictly positive denominator.</summary>
    [JsonConstructor]
    public MediaTime(long numerator, long denominator) {
        if (denominator <= 0) { throw new ArgumentOutOfRangeException(nameof(denominator)); }
        BigInteger divisor = BigInteger.GreatestCommonDivisor(numerator,denominator);
        Numerator = checked((long)(numerator/divisor));
        Denominator = checked((long)(denominator/divisor));
    }
    /// <summary>Signed numerator after reducing the fraction.</summary>
    public long Numerator { get; }
    /// <summary>Positive denominator after reducing the fraction.</summary>
    public long Denominator { get; }
    /// <summary>The exact zero value, distinct from an uninitialized struct.</summary>
    public static MediaTime Zero => new(0,1);
    /// <summary>Converts finite authored seconds to nanosecond precision once at the boundary.</summary>
    public static MediaTime FromSeconds(double seconds) {
        if (!double.IsFinite(seconds)) { throw new ArgumentOutOfRangeException(nameof(seconds)); }
        return new(checked((long)Math.Round(seconds*1000000000d,MidpointRounding.AwayFromZero)),1000000000);
    }
    /// <summary>Approximates the exact value for interpolation and external API boundaries.</summary>
    public double ToSeconds() { Validate(); return (double)Numerator/Denominator; }
    /// <summary>Rounds an exact nonnegative value upward to an integer unit count.</summary>
    public long Ceiling() {
        Validate();
        BigInteger n=Numerator; BigInteger d=Denominator;
        return checked((long)(n >= 0 ? (n+d-1)/d : n/d));
    }
    /// <summary>Tests whether a time belongs to a start-inclusive, end-exclusive interval.</summary>
    public static bool InInterval(MediaTime value,MediaTime start,MediaTime end) => value>=start && value<end;
    /// <summary>Compares fractions without overflowing intermediate long products.</summary>
    public int CompareTo(MediaTime other) { Validate(); other.Validate(); return ((BigInteger)Numerator*other.Denominator).CompareTo((BigInteger)other.Numerator*Denominator); }
    /// <summary>Compares normalized numerators and denominators.</summary>
    public bool Equals(MediaTime other) => Numerator==other.Numerator && Denominator==other.Denominator;
    /// <summary>Compares with another boxed rational value.</summary>
    public override bool Equals(object obj) => obj is MediaTime value && Equals(value);
    /// <summary>Hashes the normalized rational representation.</summary>
    public override int GetHashCode() => HashCode.Combine(Numerator,Denominator);
    /// <summary>Formats the exact rational value for CLI and diagnostics.</summary>
    public override string ToString() => $"{Numerator}/{Denominator}";
    /// <summary>Adds exact times, reducing intermediate BigInteger fractions before conversion.</summary>
    public static MediaTime operator +(MediaTime a,MediaTime b) => Create((BigInteger)a.Numerator*b.Denominator+(BigInteger)b.Numerator*a.Denominator,(BigInteger)a.Denominator*b.Denominator);
    /// <summary>Subtracts exact times without floating point drift.</summary>
    public static MediaTime operator -(MediaTime a,MediaTime b) => Create((BigInteger)a.Numerator*b.Denominator-(BigInteger)b.Numerator*a.Denominator,(BigInteger)a.Denominator*b.Denominator);
    /// <summary>Multiplies two rational values.</summary>
    public static MediaTime operator *(MediaTime a,MediaTime b) => Create((BigInteger)a.Numerator*b.Numerator,(BigInteger)a.Denominator*b.Denominator);
    /// <summary>Divides a time by a strictly positive rational rate.</summary>
    public static MediaTime operator /(MediaTime a,MediaTime b) {
        if (b.Numerator<=0) { throw new ArgumentOutOfRangeException(nameof(b)); }
        return Create((BigInteger)a.Numerator*b.Denominator,(BigInteger)a.Denominator*b.Numerator);
    }
    /// <summary>Tests normalized value equality.</summary>
    public static bool operator ==(MediaTime a,MediaTime b) => a.Equals(b);
    /// <summary>Tests normalized value inequality.</summary>
    public static bool operator !=(MediaTime a,MediaTime b) => !a.Equals(b);
    /// <summary>Tests whether the first time precedes the second.</summary>
    public static bool operator <(MediaTime a,MediaTime b) => a.CompareTo(b)<0;
    /// <summary>Tests whether the first time follows the second.</summary>
    public static bool operator >(MediaTime a,MediaTime b) => a.CompareTo(b)>0;
    /// <summary>Tests whether the first time is at or before the second.</summary>
    public static bool operator <=(MediaTime a,MediaTime b) => a.CompareTo(b)<=0;
    /// <summary>Tests whether the first time is at or after the second.</summary>
    public static bool operator >=(MediaTime a,MediaTime b) => a.CompareTo(b)>=0;
    /// <summary>Rejects uninitialized values rather than silently treating them as zero.</summary>
    public void Validate() { if (Denominator<=0) { throw new InvalidDataException("Media time requires a positive denominator."); } }
    /// <summary>Reduces arbitrary-width intermediate values and enforces stored long bounds.</summary>
    static MediaTime Create(BigInteger numerator,BigInteger denominator) {
        if (denominator<=0) { throw new InvalidDataException("Media time requires a positive denominator."); }
        BigInteger gcd=BigInteger.GreatestCommonDivisor(numerator,denominator);
        return new(checked((long)(numerator/gcd)),checked((long)(denominator/gcd)));
    }
}
