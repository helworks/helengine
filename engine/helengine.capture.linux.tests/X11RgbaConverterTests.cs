namespace helengine.capture.linux.tests;

/// <summary>Verifies packed XImage byte order, row alignment and explicit color masks.</summary>
public sealed class X11RgbaConverterTests {
    /// <summary>Little-endian BGRX pixels become opaque RGBA without copying row padding.</summary>
    [Fact] public void ConvertsPaddedLittleEndianRows() {
        byte[] actual=X11RgbaConverter.Convert([3,2,1,0,99,99,99,99,6,5,4,0,88,88,88,88],1,2,8,32,0,0xff0000,0xff00,0xff);
        Assert.Equal(new byte[]{1,2,3,255,4,5,6,255},actual);
    }
    /// <summary>The common desktop format preserves every channel across large padded rows and forces opaque alpha.</summary>
    [Fact] public void ConvertsCommonBgrxRowsWithoutIncludingPaddingOrNativeAlpha() {
        const int width=513,height=3,stride=width*4+8;
        var source=new byte[stride*height];var expected=new byte[width*height*4];
        Array.Fill(source,(byte)233);
        for(int y=0;y<height;y++) {
            for(int x=0;x<width;x++) {
                int native=y*stride+x*4,rgba=(y*width+x)*4;
                source[native]=(byte)(x*3+y);source[native+1]=(byte)(x+y*7);source[native+2]=(byte)(x*5+y*11);source[native+3]=(byte)x;
                expected[rgba]=source[native+2];expected[rgba+1]=source[native+1];expected[rgba+2]=source[native];expected[rgba+3]=255;
            }
        }
        var actual=X11RgbaConverter.Convert(source,width,height,stride,32,0,0xff0000,0xff00,0xff);
        Assert.Equal(expected,actual);Assert.Equal(233,source[stride-1]);
    }
    /// <summary>Big-endian storage follows the same visual masks rather than host endianness.</summary>
    [Fact] public void ConvertsBigEndianAndRgb565() {
        Assert.Equal(new byte[]{1,2,3,255},X11RgbaConverter.Convert([0,1,2,3],1,1,4,32,1,0xff0000,0xff00,0xff));
        Assert.Equal(new byte[]{255,0,255,255},X11RgbaConverter.Convert([0x1f,0xf8],1,1,2,16,0,0xf800,0x07e0,0x001f));
        Assert.Equal(new byte[]{3,2,1,255},X11RgbaConverter.Convert([1,2,3,0],1,1,4,24,0,0xff0000,0xff00,0xff));
    }
    /// <summary>Malformed formats fail before producing an apparently valid recording frame.</summary>
    [Fact] public void RejectsInvalidStorageAndMasks() {
        Assert.Throws<NotSupportedException>(()=>X11RgbaConverter.Convert([0,0,0,0],1,1,4,8,0,0xff,0xff00,0xff0000));
        Assert.Throws<ArgumentException>(()=>X11RgbaConverter.Convert([0,0],1,1,1,16,0,0xf800,0x7e0,0x1f));
        Assert.Throws<ArgumentException>(()=>X11RgbaConverter.Convert([0,0,0,0],1,1,4,32,0,0xff,0xff,0xff0000));
        Assert.Throws<ArgumentException>(()=>X11RgbaConverter.Convert([0,0,0,0],1,1,4,32,0,0x5,0xff00,0xff0000));
        Assert.Throws<ArgumentException>(()=>X11RgbaConverter.Convert([0,0,0,0],1,1,4,32,0,0,0xff00,0xff0000));
    }
}
