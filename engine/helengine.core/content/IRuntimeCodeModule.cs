namespace helengine {
    /// <summary>Retains a loaded native code allocation through component disposal, callback detachment and factory destruction.</summary>
    public interface IRuntimeCodeModule : IDisposable {
        /// <summary>Registers module-owned component factories and callbacks while code is loaded; partial registration must be reversible by Unregister.</summary>
        /// <param name="core">Core whose runtime services receive module callbacks.</param>
        /// <param name="registry">Registry receiving factories through RegisterForModule with this module's manifest id.</param>
        void Register(Core core, RuntimeComponentRegistry registry);
        /// <summary>Detaches module callbacks and releases module registration resources while code remains executable; factory deletion is performed next by the manager.</summary>
        /// <param name="core">Core whose subscriptions must no longer reference module code.</param>
        /// <param name="registry">Registry whose factories remain valid until manager-owned removal completes.</param>
        void Unregister(Core core, RuntimeComponentRegistry registry);
    }
}
