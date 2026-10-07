using helengine.media;
namespace helengine.media.tests;
/// <summary>Validates executable compositions and their serialization boundary.</summary>
public sealed class CompositionContractTests {
    /// <summary>Unsupported effects and absent sources never reach GPU execution.</summary>
    [Fact] public void RejectsUnknownEffectAndInvalidReference() {
        var document = Valid(); document.Layers[0].MediaId = "missing";
        document.Layers[0].Effects.Add(new MediaEffect { Id="unknown",Version=1 });
        var errors=CompositionValidator.Validate(document,MediaCapabilities.Basic());
        Assert.Contains(errors,e=>e.Code=="missing_media");
        Assert.Contains(errors,e=>e.Code=="unknown_effect");
    }
    /// <summary>Identifiers, output dimensions and effect versions are checked explicitly.</summary>
    [Fact] public void InvalidHeaderAndDuplicateIdsRejected() {
        var document=Valid(); document.Width=0;
        document.Layers.Add(document.Layers[0]);
        document.Layers[0].Effects.Add(new MediaEffect {Id="transform.2d",Version=0});
        var errors=CompositionValidator.Validate(document,MediaCapabilities.Basic());
        Assert.Contains(errors,e=>e.Code=="invalid_dimensions");
        Assert.Contains(errors,e=>e.Code=="duplicate_id");
        Assert.Contains(errors,e=>e.Code=="unknown_effect");
    }
    /// <summary>Versioned JSON retains rational time and explicit silent audio.</summary>
    [Fact] public void RoundTripKeepsExactTimesAndSilentAudio() {
        var document=Valid(); document.Duration=new(1001,24); document.Audio.Enabled=false;
        var json=CompositionJson.Serialize(document); var restored=CompositionJson.Parse(json);
        Assert.Contains("helengine.media.composition.v1",json);
        Assert.Contains("frame_rate",json); Assert.Equal(document.Duration,restored.Duration);
        Assert.False(restored.Audio.Enabled);
        Assert.Empty(CompositionValidator.Validate(restored,MediaCapabilities.Basic()));
    }
    /// <summary>Builds a small complete composition with one owned still-image source.</summary>
    static CompositionDocument Valid() => new() {
        Id="test",Revision=1,Width=540,Height=960,Duration=new(1,1),FrameRate=new(24,1),
        Audio=new CompositionAudioSettings {Enabled=false},
        Media=[new MediaReference {Id="post",Kind="image",Path="post.png",Sha256=new string('a',64),Width=685,Height=898}],
        Layers=[new VisualLayer {Id="image",MediaId="post",Start=new(0,1),End=new(1,1)}]
    };
    /// <summary>Required output configuration cannot be invented when JSON omits it.</summary>
    [Fact] public void MissingRequiredConfigurationRejected() {
        var json=CompositionJson.Serialize(Valid());
        var root=System.Text.Json.Nodes.JsonNode.Parse(json);
        root.AsObject().Remove("audio");
        Assert.Throws<System.Text.Json.JsonException>(()=>CompositionJson.Parse(root.ToJsonString()));
    }
    /// <summary>Uninitialized rational fields are returned as validation errors.</summary>
    [Fact] public void InvalidRationalAndNonFiniteGainRejected() {
        var document=Valid(); document.Duration=default;
        document.AudioClips.Add(new(){Id="voice",MediaId="post",Start=new(0,1),End=new(1,1),Gain=double.NaN});
        var errors=CompositionValidator.Validate(document,MediaCapabilities.Basic());
        Assert.Contains(errors,e=>e.Code=="invalid_timing");
        Assert.Contains(errors,e=>e.Code=="invalid_gain");
    }
}
