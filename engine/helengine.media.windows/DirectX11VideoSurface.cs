namespace helengine.media.windows;
/// <summary>Owns an RGBA GPU texture and offers readback only at an explicit boundary.</summary>
public sealed class DirectX11VideoSurface : IVideoSurface {
    /// <summary>Device borrowed from the render session, which outlives surfaces.</summary>
    readonly Device Device;
    /// <summary>Prevents duplicate texture release.</summary>
    bool Disposed;
    /// <summary>Takes ownership of one existing RGBA texture reference.</summary>
    public DirectX11VideoSurface(Device device,Texture2D texture,bool hasStoredAlpha=true,bool isLinear=false) {Device=device ?? throw new ArgumentNullException(nameof(device));Texture=texture ?? throw new ArgumentNullException(nameof(texture));Width=texture.Description.Width;Height=texture.Description.Height;HasStoredAlpha=hasStoredAlpha;IsLinear=isLinear;}
    /// <summary>Owned texture used directly by the GPU compositor.</summary>
    public Texture2D Texture {get;}
    /// <summary>Surface width in pixels.</summary>
    public int Width {get;}
    /// <summary>Surface height in pixels.</summary>
    public int Height {get;}
    /// <summary>Downloads packed RGBA pixels for encoding or explicit verification.</summary>
    public ReadOnlyMemory<byte> ReadRgba() {
        if(Disposed) {throw new ObjectDisposedException(nameof(DirectX11VideoSurface));}
        var description=Texture.Description;description.Usage=ResourceUsage.Staging;description.BindFlags=BindFlags.None;description.CpuAccessFlags=CpuAccessFlags.Read;description.OptionFlags=ResourceOptionFlags.None;
        using var staging=new Texture2D(Device,description);Device.ImmediateContext.CopyResource(Texture,staging);
        var mapped=Device.ImmediateContext.MapSubresource(staging,0,MapMode.Read,MapFlags.None);
        try {
            var bytes=new byte[checked(Width*Height*4)];
            if(description.Format==Format.R16G16B16A16_Float) {
                var rowBytes=new byte[checked(Width*8)];
                for(int row=0;row<Height;row++) {
                    Marshal.Copy(IntPtr.Add(mapped.DataPointer,row*mapped.RowPitch),rowBytes,0,rowBytes.Length);
                    for(int column=0;column<Width;column++) {for(int channel=0;channel<4;channel++) {
                        double value=(double)BitConverter.UInt16BitsToHalf(BitConverter.ToUInt16(rowBytes,column*8+channel*2));
                        if(!double.IsFinite(value)) {throw new InvalidDataException("Linear GPU surface contains a non-finite sample.");}
                        if(channel<3 && IsLinear) {value=value<=.0031308?value*12.92:1.055*Math.Pow(Math.Max(value,0),1/2.4)-.055;}
                        bytes[(row*Width+column)*4+channel]=(byte)Math.Clamp(Math.Round(value*255),0,255);
                    }}
                }
            } else {for(int row=0;row<Height;row++) {Marshal.Copy(IntPtr.Add(mapped.DataPointer,row*mapped.RowPitch),bytes,row*Width*4,Width*4);}}
            return bytes;
        }
        finally {Device.ImmediateContext.UnmapSubresource(staging,0);}
    }
    /// <summary>Releases this surface's texture reference exactly once.</summary>
    public void Dispose() {if(!Disposed) {Disposed=true;Texture.Dispose();}}
    /// <summary>Whether original input storage carried alpha before texture expansion.</summary>
    public bool HasStoredAlpha {get;}
    /// <summary>Whether texture RGB is already linear rather than encoded sRGB.</summary>
    public bool IsLinear {get;}
}
