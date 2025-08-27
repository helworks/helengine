using System.Reflection;

namespace Nucleus {
    /// <summary>
    /// Holds shared information for the whole library
    /// </summary>
    public static class Globals {
        public const bool Alpha = true;

        /// <summary>
        /// TODO: fix major to correct version
        /// </summary>
        public static int Version {
            get {
                Assembly ass = Assembly.GetEntryAssembly();
                return ass.GetName().Version.Major;
            }
        }

        // public static string Name {
        //     get {
        //         Assembly ass = Assembly.GetEntryAssembly();
        //         return ass.GetName().Name;
        //     }
        // }
    }
}
