namespace helengine.media;
/// <summary>Maps an oriented source through fit, viewport padding and a bounded zoom crop.</summary>
public sealed class PresentationMapping {
    /// <summary>Stores fitted image offsets and the crop of the already-fitted viewport.</summary>
    public PresentationMapping(double imageX,double imageY,double fittedWidth,double fittedHeight,double cropX,double cropY,double cropWidth,double cropHeight,double zoom) {ImageX=imageX;ImageY=imageY;FittedWidth=fittedWidth;FittedHeight=fittedHeight;CropX=cropX;CropY=cropY;CropWidth=cropWidth;CropHeight=cropHeight;Zoom=zoom;}
    /// <summary>Image left edge before zoom, including contain padding or cover cropping.</summary>
    public double ImageX {get;}
    /// <summary>Image top edge before zoom.</summary>
    public double ImageY {get;}
    /// <summary>Image width after the initial fit.</summary>
    public double FittedWidth {get;}
    /// <summary>Image height after the initial fit.</summary>
    public double FittedHeight {get;}
    /// <summary>Left edge of the zoom sampling region in the fitted viewport.</summary>
    public double CropX {get;}
    /// <summary>Top edge of the zoom sampling region in the fitted viewport.</summary>
    public double CropY {get;}
    /// <summary>Width of the zoom sampling region in the fitted viewport.</summary>
    public double CropWidth {get;}
    /// <summary>Height of the zoom sampling region in the fitted viewport.</summary>
    public double CropHeight {get;}
    /// <summary>Magnification applied after fitting and padding.</summary>
    public double Zoom {get;}
    /// <summary>Maps normalized source coordinates into output viewport pixels.</summary>
    public PresentationPoint MapSourcePoint(double x,double y) => new((ImageX+x*FittedWidth-CropX)*Zoom,(ImageY+y*FittedHeight-CropY)*Zoom);
}
