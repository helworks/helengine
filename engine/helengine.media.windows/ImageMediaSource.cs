using System.Drawing;
using System.Drawing.Imaging;
namespace helengine.media.windows;
/// <summary>Loads an immutable oriented image once and lends independently owned texture references.</summary>
public sealed class ImageMediaSource : IMediaSource {
    /// <summary>Source file lease owned by this cursor.</summary>
    readonly OwnedMediaFile File;
    /// <summary>Graphics device borrowed from the render session.</summary>
    readonly Device Device;
    /// <summary>Immutable image texture reused across timeline frames.</summary>
    readonly Texture2D Texture;
    /// <summary>Original image storage alpha, distinct from synthesized JPEG opacity.</summary>
    readonly bool HasStoredAlpha;
    /// <summary>Prevents repeated release of image and file resources.</summary>
    bool Disposed;
    /// <summary>Decodes PNG/JPG in exact pixel dimensions regardless of printing DPI and applies EXIF orientation before upload.</summary>
    public ImageMediaSource(OwnedMediaFile file,MediaReference reference,Device device) {
        File=file;Device=device;
        if(file.Stream.Length>33554432) {throw new InvalidDataException("Image exceeds 32 MiB.");}
        using var image=System.Drawing.Image.FromStream(file.Stream,false,true);HasStoredAlpha=System.Drawing.Image.IsAlphaPixelFormat(image.PixelFormat);
        if(image.Width!=reference.Width || image.Height!=reference.Height || (long)image.Width*image.Height>33554432) {throw new InvalidDataException("Image dimensions do not match the pinned source.");}
        if(image.PropertyIdList.Contains(0x112)) {int orientation=BitConverter.ToUInt16(image.GetPropertyItem(0x112).Value,0);image.RotateFlip(Orientation(orientation));}
        using var bitmap=new Bitmap(image.Width,image.Height,PixelFormat.Format32bppArgb);
        using(var graphics=Graphics.FromImage(bitmap)) {graphics.CompositingMode=System.Drawing.Drawing2D.CompositingMode.SourceCopy;graphics.DrawImage(image,new Rectangle(0,0,bitmap.Width,bitmap.Height),0,0,image.Width,image.Height,GraphicsUnit.Pixel);}
        var data=bitmap.LockBits(new Rectangle(0,0,bitmap.Width,bitmap.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
        var pixels=new byte[checked(bitmap.Width*bitmap.Height*4)];
        try {for(int row=0;row<bitmap.Height;row++) {Marshal.Copy(IntPtr.Add(data.Scan0,row*data.Stride),pixels,row*bitmap.Width*4,bitmap.Width*4);}}
        finally {bitmap.UnlockBits(data);}
        for(int index=0;index<pixels.Length;index+=4) {byte blue=pixels[index];pixels[index]=pixels[index+2];pixels[index+2]=blue;}
        var pinned=GCHandle.Alloc(pixels,GCHandleType.Pinned);
        try {var description=new Texture2DDescription {Width=bitmap.Width,Height=bitmap.Height,MipLevels=1,ArraySize=1,Format=Format.R8G8B8A8_UNorm,SampleDescription=new(1,0),Usage=ResourceUsage.Immutable,BindFlags=BindFlags.ShaderResource};Texture=new Texture2D(device,description,[new SharpDX.DataBox(pinned.AddrOfPinnedObject(),bitmap.Width*4,0)]);}
        finally {pinned.Free();}
    }
    /// <summary>Returns an independently releasable COM reference to the immutable image texture.</summary>
    public MediaVideoFrame ReadVideoFrame(MediaTime sourceTime) {
        if(Disposed) {throw new ObjectDisposedException(nameof(ImageMediaSource));}
        sourceTime.Validate();Marshal.AddRef(Texture.NativePointer);
        return new(sourceTime,MediaTime.Zero,new DirectX11VideoSurface(Device,new Texture2D(Texture.NativePointer),HasStoredAlpha));
    }
    /// <summary>Still images have no sound and report explicit silence.</summary>
    public AudioBlock ReadAudio(long first,int count,AudioFormat target) {if(Disposed) {throw new ObjectDisposedException(nameof(ImageMediaSource));}if(first<0 || count<0 || count>65536) {throw new ArgumentOutOfRangeException(nameof(count));}return new(first,new float[checked(count*target.Channels)],target,true);}
    /// <summary>Releases the immutable texture and verified file lease once.</summary>
    public void Dispose() {if(!Disposed) {Disposed=true;Texture.Dispose();File.Dispose();}}
    /// <summary>Maps the standard EXIF orientations to their pixel transform.</summary>
    static RotateFlipType Orientation(int orientation) => orientation switch {2=>RotateFlipType.RotateNoneFlipX,3=>RotateFlipType.Rotate180FlipNone,4=>RotateFlipType.Rotate180FlipX,5=>RotateFlipType.Rotate90FlipX,6=>RotateFlipType.Rotate90FlipNone,7=>RotateFlipType.Rotate270FlipX,8=>RotateFlipType.Rotate270FlipNone,_=>RotateFlipType.RotateNoneFlipNone};
}
