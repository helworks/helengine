namespace helengine.media.tests;
/// <summary>Rejects impossible visual sources before opening media or allocating a GPU pass.</summary>
public sealed class VisualSourceValidationTests {
    /// <summary>An audio-only file cannot satisfy a visual layer binding.</summary>
    [Fact] public void AudioCannotBeUsedAsPicture() {var document=new CompositionDocument{Id="invalid",Revision=1,Width=32,Height=32,FrameRate=new(24,1),Duration=new(2,1),Media=[new(){Id="voice",Kind="audio",Path="voice.wav",Sha256=new string('a',64),Duration=new(2,1)}],Layers=[new(){Id="picture",MediaId="voice",Start=MediaTime.Zero,End=new(2,1)}]};Assert.Contains(CompositionValidator.Validate(document,MediaCapabilities.Basic()),error=>error.Code=="invalid_visual_source");}
    /// <summary>A timed mask needs real handles through the whole layer interval, not an implicit frozen last frame.</summary>
    [Fact] public void TimedMaskRequiresCompleteSourceHandles() {var document=new CompositionDocument{Id="mask",Revision=1,Width=32,Height=32,FrameRate=new(24,1),Duration=new(2,1),Media=[new(){Id="post",Kind="image",Path="post.png",Sha256=new string('a',64),Width=32,Height=32},new(){Id="mask",Kind="video",Path="mask.mp4",Sha256=new string('b',64),Width=32,Height=32,Duration=new(1,1)}],Layers=[new(){Id="picture",MediaId="post",Start=MediaTime.Zero,End=new(2,1),Mask=new(){MediaId="mask"}}]};Assert.Contains(CompositionValidator.Validate(document,MediaCapabilities.Basic()),error=>error.Code=="visual_input_handles");}
}
