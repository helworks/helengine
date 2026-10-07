namespace helengine.media;
/// <summary>Computes fit-before-zoom mappings so source padding survives editorial motion.</summary>
public static class PresentationTransform {
    /// <summary>Resolves a contained or covered source and clamps zoom within the original viewport.</summary>
    public static PresentationMapping Resolve(int sourceWidth,int sourceHeight,double viewportWidth,double viewportHeight,string fit,double zoom,double focusX,double focusY) {
        if(sourceWidth<=0 || sourceHeight<=0 || !double.IsFinite(viewportWidth+viewportHeight+zoom+focusX+focusY) || viewportWidth<=0 || viewportHeight<=0 || zoom<1 || focusX<0 || focusX>1 || focusY<0 || focusY>1) {throw new InvalidDataException("Invalid presentation transform.");}
        double scale=fit switch {"contain"=>Math.Min(viewportWidth/sourceWidth,viewportHeight/sourceHeight),"cover"=>Math.Max(viewportWidth/sourceWidth,viewportHeight/sourceHeight),_=>throw new InvalidDataException("Unsupported fit mode.")};
        double width=sourceWidth*scale;double height=sourceHeight*scale;double x=(viewportWidth-width)/2;double y=(viewportHeight-height)/2;
        double cropWidth=viewportWidth/zoom;double cropHeight=viewportHeight/zoom;
        double cropX=Math.Clamp(x+focusX*width-cropWidth/2,0,viewportWidth-cropWidth);double cropY=Math.Clamp(y+focusY*height-cropHeight/2,0,viewportHeight-cropHeight);
        return new(x,y,width,height,cropX,cropY,cropWidth,cropHeight,zoom);
    }
}
