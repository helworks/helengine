namespace helengine.vfx {
    /// <summary>
    /// Maps effect ids to registered effect instances. New effects register themselves here at startup.
    /// </summary>
    public static class VfxEffectRegistry {
        static readonly Dictionary<string, IVfxEffect> effects = new Dictionary<string, IVfxEffect>();

        public static void Register(IVfxEffect effect) {
            if (effect == null) {
                throw new ArgumentNullException(nameof(effect));
            }
            effects[effect.Id] = effect;
        }

        public static IVfxEffect Resolve(string id) {
            if (effects.TryGetValue(id, out IVfxEffect effect)) {
                return effect;
            }
            string knownIds = string.Join(", ", effects.Keys);
            throw new InvalidOperationException($"No VFX effect is registered with id '{id}'. Known effect ids: {knownIds}");
        }

        public static IReadOnlyCollection<string> KnownIds => effects.Keys;
    }
}
