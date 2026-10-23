namespace helengine.capture.linux;

/// <summary>Converts supported TrueColor XImage storage into independent, tightly packed opaque RGBA.</summary>
static class X11RgbaConverter {
    /// <summary>Converts each pixel using the server's stride, byte order and visual channel masks.</summary>
    internal static byte[] Convert(byte[] source,int width,int height,int stride,int bitsPerPixel,int byteOrder,ulong redMask,ulong greenMask,ulong blueMask) {
        if(bitsPerPixel is not (16 or 24 or 32)) { throw new NotSupportedException($"XImage storage of {bitsPerPixel} bits per pixel is unsupported."); }
        if(byteOrder is not (0 or 1)) { throw new NotSupportedException("Unsupported XImage byte order."); }
        int bytesPerPixel=bitsPerPixel/8;
        if(width<=0 || height<=0 || stride<checked(width*bytesPerPixel) || source.Length<checked(stride*height)) { throw new ArgumentException("XImage dimensions, row stride or storage length are invalid."); }
        ValidateMask(redMask,bitsPerPixel);ValidateMask(greenMask,bitsPerPixel);ValidateMask(blueMask,bitsPerPixel);
        if((redMask&greenMask)!=0 || (redMask&blueMask)!=0 || (greenMask&blueMask)!=0) { throw new ArgumentException("XImage color masks overlap."); }
        var result=new byte[checked(width*height*4)];
        for(int y=0;y<height;y++) {
            for(int x=0;x<width;x++) {
                int start=y*stride+x*bytesPerPixel;
                ulong pixel=0;
                for(int b=0;b<bytesPerPixel;b++) {
                    int shift=byteOrder==0 ? b*8 : (bytesPerPixel-b-1)*8;
                    pixel|=(ulong)source[start+b]<<shift;
                }
                int target=(y*width+x)*4;
                result[target]=Channel(pixel,redMask);result[target+1]=Channel(pixel,greenMask);result[target+2]=Channel(pixel,blueMask);result[target+3]=255;
            }
        }
        return result;
    }
    /// <summary>Rejects absent, noncontiguous or out-of-range TrueColor channel masks.</summary>
    static void ValidateMask(ulong mask,int bitsPerPixel) {
        if(mask==0 || (mask>>bitsPerPixel)!=0) { throw new ArgumentException("XImage color mask is absent or exceeds its pixel storage."); }
        ulong normalized=mask>>System.Numerics.BitOperations.TrailingZeroCount(mask);
        if((normalized&(normalized+1))!=0) { throw new ArgumentException("XImage color mask must be contiguous."); }
    }
    /// <summary>Scales a packed channel to eight bits with nearest-integer rounding.</summary>
    static byte Channel(ulong pixel,ulong mask) {
        int shift=System.Numerics.BitOperations.TrailingZeroCount(mask);
        ulong maximum=mask>>shift;
        return (byte)((((pixel&mask)>>shift)*255+maximum/2)/maximum);
    }
}
