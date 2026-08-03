using helengine.vfx;
using helengine.vfx.directx11;
using helengine.vfx.io;

namespace helengine.vfx.cli {
    /// <summary>
    /// Drives one CLI invocation end to end: argument parsing, help output, effect and parameter
    /// validation, clip discovery, and the GPU export run. Everything that is not process plumbing
    /// lives here so the entry point stays a single call and so the validation paths stay testable
    /// without a GPU.
    /// </summary>
    public static class VfxCliRunner {
        /// <summary>
        /// Executes one CLI invocation.
        /// </summary>
        /// <param name="args">Raw process arguments.</param>
        /// <returns>Process exit code; 0 on success, 1 on any caller-facing failure.</returns>
        public static int Run(string[] args) {
            if (args == null) {
                throw new ArgumentNullException(nameof(args));
            }

            if (!VfxCliArguments.TryParse(args, out VfxCliArguments parsedArgs, out string parseError)) {
                Console.Error.WriteLine(parseError);
                return 1;
            }

            if (parsedArgs.ShowHelp) {
                return WriteHelp(parsedArgs.EffectId);
            }

            IVfxEffect effect;
            try {
                effect = VfxEffectRegistry.Resolve(parsedArgs.EffectId);
            } catch (InvalidOperationException ex) {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }

            // Parameter names and values are validated here, before the Direct3D11 device exists, so a
            // typo costs nothing and reports cleanly rather than surfacing as a stack trace after
            // device creation and shader compilation.
            if (!VfxCliParameterValidator.TryValidate(effect, parsedArgs.ParameterValues, out string parameterError)) {
                Console.Error.WriteLine(parameterError);
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
        }

        /// <summary>
        /// Writes the general help block, plus the parameter listing for one effect when the caller
        /// combined <c>--help</c> with <c>--effect</c>.
        /// </summary>
        /// <param name="effectId">Effect to describe, or null to print general help only.</param>
        /// <returns>Process exit code; 0 when help was printed, 1 when the named effect is unknown.</returns>
        static int WriteHelp(string effectId) {
            Console.WriteLine(VfxCliHelpText.BuildGeneralHelp());

            if (string.IsNullOrWhiteSpace(effectId)) {
                return 0;
            }

            IVfxEffect effect;
            try {
                effect = VfxEffectRegistry.Resolve(effectId);
            } catch (InvalidOperationException ex) {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }

            Console.WriteLine();
            Console.Write(VfxCliHelpText.BuildEffectHelp(effect));
            return 0;
        }
    }
}
