namespace helengine.vfx {
    /// <summary>
    /// Rejects effect definitions that could not execute deterministically: missing names, passes that read a target
    /// before any pass wrote it, a final pass that does not produce the output, and overlapping or out-of-range
    /// parameter slots. Runs before any GPU resource exists, so a broken <c>.heffect</c> fails with a clear message.
    /// </summary>
    public static class VfxEffectValidator {
        /// <summary>
        /// Validates one effect definition.
        /// </summary>
        /// <param name="effect">Effect to check.</param>
        /// <exception cref="InvalidDataException">The definition is inconsistent; the message names the problem.</exception>
        public static void Validate(EffectAsset effect) {
            if (effect == null) {
                throw new ArgumentNullException(nameof(effect));
            }
            string id = effect.EffectId;
            if (string.IsNullOrWhiteSpace(id)) {
                throw new InvalidDataException("Effect requires an effect id.");
            }
            if (effect.EffectVersion < 1) {
                throw new InvalidDataException($"Effect '{id}' version must be at least 1.");
            }
            if (effect.Inputs == null || effect.Inputs.Length == 0) {
                throw new InvalidDataException($"Effect '{id}' must declare at least one input; the first is its main input.");
            }
            if (!Enum.IsDefined(effect.Category)) {
                throw new InvalidDataException($"Effect '{id}' has an unknown category.");
            }
            if (effect.Category == EffectCategory.Transition && effect.Inputs.Length != 2) {
                throw new InvalidDataException($"Transition '{id}' must declare exactly two inputs: the outgoing scene, then the incoming scene.");
            }
            if (effect.Passes == null || effect.Passes.Length == 0) {
                throw new InvalidDataException($"Effect '{id}' must declare at least one pass.");
            }

            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal) { EffectAsset.OutputTargetName };
            foreach (EffectInputAsset input in effect.Inputs) {
                AddName(names, input?.Name, id);
            }
            foreach (EffectTargetAsset target in effect.Targets ?? Array.Empty<EffectTargetAsset>()) {
                AddName(names, target?.Name, id);
                if (!float.IsFinite(target.Scale) || target.Scale <= 0f || target.Scale > 1f) {
                    throw new InvalidDataException($"Effect '{id}' target '{target.Name}' scale must be greater than 0 and at most 1.");
                }
                if (!Enum.IsDefined(target.Format)) {
                    throw new InvalidDataException($"Effect '{id}' target '{target.Name}' has an unknown format.");
                }
            }

            ValidatePasses(effect, id);
            ValidateParameters(effect, id);
        }

        /// <summary>
        /// Adds one input or target name, rejecting blanks, duplicates and the reserved output name.
        /// </summary>
        /// <param name="names">Names declared so far.</param>
        /// <param name="name">Name to add.</param>
        /// <param name="id">Effect id used in error messages.</param>
        static void AddName(HashSet<string> names, string name, string id) {
            if (string.IsNullOrWhiteSpace(name)) {
                throw new InvalidDataException($"Effect '{id}' has an input or target without a name.");
            }
            if (!names.Add(name)) {
                throw new InvalidDataException($"Effect '{id}' declares '{name}' more than once or reuses the reserved '{EffectAsset.OutputTargetName}' name.");
            }
        }

        /// <summary>
        /// Checks that every pass reads only images that already hold data and that the last pass writes the output.
        /// </summary>
        /// <param name="effect">Effect being validated.</param>
        /// <param name="id">Effect id used in error messages.</param>
        static void ValidatePasses(EffectAsset effect, string id) {
            HashSet<string> targets = new HashSet<string>((effect.Targets ?? Array.Empty<EffectTargetAsset>()).Select(target => target.Name), StringComparer.Ordinal);
            HashSet<string> readable = new HashSet<string>(effect.Inputs.Select(input => input.Name), StringComparer.Ordinal);
            for (int index = 0; index < effect.Passes.Length; index++) {
                EffectPassAsset pass = effect.Passes[index];
                if (pass == null || string.IsNullOrWhiteSpace(pass.ShaderPath) || string.IsNullOrWhiteSpace(pass.PixelEntryPoint)) {
                    throw new InvalidDataException($"Effect '{id}' pass {index} requires a shader path and pixel entry point.");
                }
                if (Path.IsPathRooted(pass.ShaderPath) || pass.ShaderPath.Split('/', '\\').Contains("..")) {
                    throw new InvalidDataException($"Effect '{id}' pass {index} shader path must stay inside its shader root.");
                }
                foreach (string read in pass.Reads ?? Array.Empty<string>()) {
                    if (!readable.Contains(read)) {
                        throw new InvalidDataException($"Effect '{id}' pass {index} reads '{read}' before it is an input or a written target.");
                    }
                }
                bool writesOutput = pass.Writes == EffectAsset.OutputTargetName;
                if (!writesOutput && !targets.Contains(pass.Writes ?? string.Empty)) {
                    throw new InvalidDataException($"Effect '{id}' pass {index} writes undeclared target '{pass.Writes}'.");
                }
                if ((pass.Reads ?? Array.Empty<string>()).Contains(pass.Writes)) {
                    throw new InvalidDataException($"Effect '{id}' pass {index} cannot read and write '{pass.Writes}' at once.");
                }
                if (writesOutput != (index == effect.Passes.Length - 1)) {
                    throw new InvalidDataException($"Effect '{id}' must write '{EffectAsset.OutputTargetName}' exactly in its last pass.");
                }
                readable.Add(pass.Writes);
            }
        }

        /// <summary>
        /// Checks parameter names, types, bounds, defaults and slot packing.
        /// </summary>
        /// <param name="effect">Effect being validated.</param>
        /// <param name="id">Effect id used in error messages.</param>
        static void ValidateParameters(EffectAsset effect, string id) {
            bool[] used = new bool[VfxFrameConstants.ParamSlotCount];
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            foreach (EffectParameterAsset parameter in effect.Parameters ?? Array.Empty<EffectParameterAsset>()) {
                if (parameter == null || string.IsNullOrWhiteSpace(parameter.Name) || !names.Add(parameter.Name)) {
                    throw new InvalidDataException($"Effect '{id}' has a parameter without a unique name.");
                }
                if (!Enum.IsDefined(parameter.Type)) {
                    throw new InvalidDataException($"Effect '{id}' parameter '{parameter.Name}' has an unknown type.");
                }
                if (!float.IsFinite(parameter.Minimum) || !float.IsFinite(parameter.Maximum) || parameter.Minimum > parameter.Maximum) {
                    throw new InvalidDataException($"Effect '{id}' parameter '{parameter.Name}' has an invalid range.");
                }
                int count = VfxParameterLayout.SlotCount(parameter.Type);
                if (parameter.Slot < 0 || parameter.Slot + count > VfxFrameConstants.ParamSlotCount) {
                    throw new InvalidDataException($"Effect '{id}' parameter '{parameter.Name}' does not fit in slots 0 to {VfxFrameConstants.ParamSlotCount - 1}.");
                }
                for (int slot = parameter.Slot; slot < parameter.Slot + count; slot++) {
                    if (used[slot]) {
                        throw new InvalidDataException($"Effect '{id}' parameter '{parameter.Name}' overlaps slot {slot}.");
                    }
                    used[slot] = true;
                }
                if (parameter.Type == EffectParameterType.Enum) {
                    int length = parameter.AllowedValues?.Length ?? 0;
                    if (length == 0 || parameter.DefaultValue.X < 0 || parameter.DefaultValue.X >= length || parameter.DefaultValue.X != Math.Floor(parameter.DefaultValue.X)) {
                        throw new InvalidDataException($"Effect '{id}' enum parameter '{parameter.Name}' needs allowed values and a valid default index.");
                    }
                } else {
                    float[] defaults = { parameter.DefaultValue.X, parameter.DefaultValue.Y, parameter.DefaultValue.Z, parameter.DefaultValue.W };
                    for (int component = 0; component < count; component++) {
                        float value = defaults[component];
                        if (value < parameter.Minimum || value > parameter.Maximum || (VfxParameterLayout.IsWholeNumber(parameter.Type) && value != Math.Floor(value))) {
                            throw new InvalidDataException($"Effect '{id}' parameter '{parameter.Name}' default is outside its declared range.");
                        }
                    }
                }
            }
        }
    }
}
