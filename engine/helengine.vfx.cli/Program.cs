using helengine.vfx;
using helengine.vfx.cli;
using helengine.vfx.directx11;
using helengine.vfx.effects;
using helengine.vfx.io;

VfxEffectRegistry.Register(new RainbowExpandEffect());

if (!VfxCliArguments.TryParse(args, out VfxCliArguments parsedArgs, out string parseError)) {
    Console.Error.WriteLine(parseError);
    return 1;
}

IVfxEffect effect;
try {
    effect = VfxEffectRegistry.Resolve(parsedArgs.EffectId);
} catch (InvalidOperationException ex) {
    Console.Error.WriteLine(ex.Message);
    return 1;
}

VfxClip clip;
try {
    ImageSequence source = ExrSequenceReader.ReadSequence(parsedArgs.SourceFolder);
    ImageSequence mask = ExrSequenceReader.ReadSequence(parsedArgs.MaskFolder);
    clip = new VfxClip(source, mask);
} catch (Exception ex) when (ex is InvalidOperationException || ex is DirectoryNotFoundException) {
    Console.Error.WriteLine(ex.Message);
    return 1;
}

using (var vfxDevice = new DirectX11VfxDevice())
using (var runner = new DirectX11VfxEffectRunner(vfxDevice, effect)) {
    runner.Run(clip, effect, parsedArgs.ParameterValues, parsedArgs.OutputFolder);
}

Console.WriteLine($"Wrote {clip.FrameCount} frame(s) to '{parsedArgs.OutputFolder}'.");
return 0;
