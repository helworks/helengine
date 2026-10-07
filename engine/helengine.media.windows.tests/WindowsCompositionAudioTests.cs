using System.Security.Cryptography;
using helengine.media;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using NAudio.Wave;
namespace helengine.media.windows.tests;
/// <summary>Checks native impulse PCM through the real Windows pull provider and output device clock.</summary>
public sealed class WindowsCompositionAudioTests {
    /// <summary>Native impulse survives the mixed provider at exactly one second.</summary>
    [Fact] public void ImpulseProviderMatchesOfflinePcm() {
        const string root="C:/dev/helworks/builds/helengine/media-composition/fixtures";var document=new CompositionDocument{Id="impulse",Revision=1,Width=32,Height=32,Duration=new(2,1),FrameRate=new(24,1),Media=[new(){Id="impulse",Kind="audio",Path="sync-impulse.wav",Sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root,"sync-impulse.wav")))).ToLowerInvariant(),Duration=new(2,1)}],AudioClips=[new(){Id="voice",MediaId="impulse",Start=MediaTime.Zero,End=new(2,1),SourceOut=new(2,1)}]};
        using var device=new Device(DriverType.Warp,DeviceCreationFlags.BgraSupport);using var resolver=new WindowsMediaSourceResolver(root,device);using var mixer=new CompositionAudioMixer(resolver);var provider=new WindowsCompositionPcmProvider(new(48000,2),7,(first,count,generation)=>mixer.Render(document,first,count).Block,CancellationToken.None);byte[] bytes=new byte[96000*8];Assert.Equal(bytes.Length,provider.Read(bytes,0,bytes.Length));Assert.Equal(.75f,BitConverter.ToSingle(bytes,48000*8));Assert.Equal(0,BitConverter.ToSingle(bytes,47999*8));Assert.Equal(0,provider.Read(bytes,0,8));
    }
    /// <summary>The device clock reports consumed PCM while the bounded buffers remain separate.</summary>
    [Fact] public async Task WindowsDeviceClockConsumesSingleStream() {
        using var output=new WindowsCompositionAudioOutput();output.Reset(new(48000,2),1,(first,count,generation)=>new(first,new float[count*2],new(48000,2),true),CancellationToken.None);Assert.Equal(0,output.ConsumedSampleFrames);output.Play();await Task.Delay(150);output.Pause();Assert.InRange(output.ConsumedSampleFrames,1,24000);long old=output.Generation;output.Reset(new(48000,2),2,(first,count,generation)=>new(first,new float[count*2],new(48000,2),true),CancellationToken.None);Assert.NotEqual(old,output.Generation);Assert.Equal(0,output.ConsumedSampleFrames);
    }
}
