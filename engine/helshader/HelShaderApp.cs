namespace helshader {
    /// <summary>
    /// Orchestrates helshader command parsing and execution.
    /// </summary>
    public class HelShaderApp {
        /// <summary>
        /// Runs the tool with the provided arguments.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        /// <returns>Exit code indicating success or failure.</returns>
        public int Run(string[] args) {
            ShaderCommandLineParser parser = new ShaderCommandLineParser();
            ShaderCommandOptions options = parser.Parse(args);
            if (options.Command == ShaderCommandType.None) {
                PrintUsage();
                return 1;
            }

            try {
                if (options.Command == ShaderCommandType.Build) {
                    ShaderBuildCommand command = new ShaderBuildCommand();
                    command.Execute(options);
                    return 0;
                }

                if (options.Command == ShaderCommandType.Codegen) {
                    ShaderCodegenCommand command = new ShaderCodegenCommand();
                    command.Execute(options);
                    return 0;
                }

                if (options.Command == ShaderCommandType.Validate) {
                    ShaderValidateCommand command = new ShaderValidateCommand();
                    command.Execute(options);
                    return 0;
                }
            } catch (Exception ex) {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }

            PrintUsage();
            return 1;
        }

        /// <summary>
        /// Prints usage information to standard output.
        /// </summary>
        void PrintUsage() {
            Console.WriteLine("helshader build --manifest <path> [--shader <name>] [--target <name>] [--variant <name>] [--emit-modules]");
            Console.WriteLine("helshader codegen --manifest <path> [--shader <name>]");
            Console.WriteLine("helshader validate --manifest <path>");
        }
    }
}
