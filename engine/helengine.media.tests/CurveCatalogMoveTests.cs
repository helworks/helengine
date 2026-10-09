namespace helengine.media.tests;
/// <summary>Locks the move of the curve formulas into <see cref="CurveCatalog"/>: media results stay bit-identical to the formulas media shipped before the move, and the catalog's id and code views agree.</summary>
public sealed class CurveCatalogMoveTests {
    /// <summary>Every catalog curve id.</summary>
    public static TheoryData<string> CurveIds => new TheoryData<string> {"linear.v1","smoothstep.v1","ease_out_cubic.v1","ease_in_quad.v1","ease_out_back.v1"};
    /// <summary>Media and catalog return exactly the pre-move values on a dense grid, including clamped progress outside zero..one.</summary>
    [Theory, MemberData(nameof(CurveIds))]
    public void MediaCurveMatchesThePreMoveFormulasOnAGrid(string id) {
        Assert.True(MediaCurve.Supports(id));
        for(int step=-10;step<=210;step++) {
            double progress=step/200.0;
            double expected=PreMoveFormula(id,progress);
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected),BitConverter.DoubleToInt64Bits(MediaCurve.Evaluate(id,progress)));
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected),BitConverter.DoubleToInt64Bits(CurveCatalog.Evaluate(CurveCatalog.GetCode(id),progress)));
        }
    }
    /// <summary>Codes are stable and map back to their ids.</summary>
    [Fact]
    public void CodesAreStableAndReversible() {
        Assert.Equal(5,CurveCatalog.Count);
        Assert.Equal((byte)0,CurveCatalog.GetCode("linear.v1"));
        Assert.Equal((byte)4,CurveCatalog.GetCode("ease_out_back.v1"));
        for(byte code=0;code<CurveCatalog.Count;code++) {Assert.Equal(code,CurveCatalog.GetCode(CurveCatalog.GetId(code)));}
    }
    /// <summary>Unknown ids and non-finite progress keep failing the way media always did.</summary>
    [Fact]
    public void UnknownCurvesAndNonFiniteProgressStillThrow() {
        Assert.False(MediaCurve.Supports("bounce.v1"));
        Assert.False(MediaCurve.Supports(null));
        Assert.Throws<InvalidDataException>(() => MediaCurve.Evaluate("bounce.v1",.5));
        Assert.Throws<ArgumentOutOfRangeException>(() => MediaCurve.Evaluate("linear.v1",double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => MediaCurve.Evaluate("bounce.v1",double.PositiveInfinity));
    }
    /// <summary>Copy of the formulas <c>MediaCurve</c> evaluated before the move, kept as the reference.</summary>
    static double PreMoveFormula(string id,double progress) {
        double value=Math.Clamp(progress,0,1);
        if(id=="linear.v1") {return value;}
        if(id=="smoothstep.v1") {return value*value*(3-2*value);}
        if(id=="ease_out_cubic.v1") {return 1-Math.Pow(1-value,3);}
        if(id=="ease_in_quad.v1") {return value*value;}
        const double overshoot=1.70158;
        double shifted=value-1;
        return 1+(overshoot+1)*shifted*shifted*shifted+overshoot*shifted*shifted;
    }
}
