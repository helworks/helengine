namespace helengine.vfx {
    /// <summary>
    /// Maps effect ids to registered effect instances. New effects register themselves here at startup.
    /// </summary>
    public static class VfxEffectRegistry {
        /// <summary>
        /// Registered effects keyed by their <see cref="IVfxEffect.Id"/>.
        /// </summary>
        static readonly Dictionary<string, IVfxEffect> Effects = new Dictionary<string, IVfxEffect>();

        /// <summary>
        /// Ids of every currently registered effect, in registration order.
        /// </summary>
        public static IReadOnlyCollection<string> KnownIds => Effects.Keys;

        /// <summary>
        /// Registers an effect, replacing any previous registration that used the same id.
        /// </summary>
        /// <param name="effect">Effect instance to make resolvable by its id.</param>
        public static void Register(IVfxEffect effect) {
            if (effect == null) {
                throw new ArgumentNullException(nameof(effect));
            }
            Effects[effect.Id] = effect;
        }

        /// <summary>
        /// Looks up a registered effect by id.
        /// </summary>
        /// <param name="id">Effect id to resolve.</param>
        /// <returns>The registered effect instance.</returns>
        public static IVfxEffect Resolve(string id) {
            if (Effects.TryGetValue(id, out IVfxEffect effect)) {
                return effect;
            }
            string knownIds = string.Join(", ", Effects.Keys);
            throw new InvalidOperationException($"No VFX effect is registered with id '{id}'. Known effect ids: {knownIds}");
        }
    }
}
