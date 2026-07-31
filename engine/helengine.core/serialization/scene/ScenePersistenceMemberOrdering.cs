#if !HELENGINE_CODEGEN_DISABLE_RUNTIME_SCRIPT_REFLECTION
using System.Reflection;

namespace helengine {
    /// <summary>
    /// Produces the shared stable member order used by editor persistence and runtime ordinal deserialization.
    /// </summary>
    public static class ScenePersistenceMemberOrdering {
        /// <summary>
        /// Orders required members alphabetically and append-only members by contiguous immutable compatibility ordinal.
        /// </summary>
        /// <param name="members">Persisted reflected members to validate and order.</param>
        /// <returns>Required members followed by the validated append-only suffix.</returns>
        public static MemberInfo[] OrderMembers(IEnumerable<MemberInfo> members) {
            if (members == null) {
                throw new ArgumentNullException(nameof(members));
            }

            MemberInfo[] orderedMembers = members
                .OrderBy(member => IsAppended(member) ? 1 : 0)
                .ThenBy(GetOrderingValue)
                .ThenBy(member => member.Name, StringComparer.Ordinal)
                .ToArray();
            ValidateAppendOrders(orderedMembers);
            return orderedMembers;
        }

        /// <summary>
        /// Determines whether one reflected member belongs to the optional append-only payload suffix.
        /// </summary>
        /// <param name="member">Reflected persisted member to inspect.</param>
        /// <returns>True when the member declares an append compatibility ordinal; otherwise false.</returns>
        public static bool IsAppended(MemberInfo member) {
            if (member == null) {
                throw new ArgumentNullException(nameof(member));
            }

            return member.IsDefined(typeof(ScenePersistenceAppendAttribute), false);
        }

        /// <summary>
        /// Returns the alphabetical ordering placeholder for required members or the explicit ordinal for appended members.
        /// </summary>
        /// <param name="member">Reflected member being ordered.</param>
        /// <returns>Zero for required members or the declared append ordinal.</returns>
        static int GetOrderingValue(MemberInfo member) {
            ScenePersistenceAppendAttribute attribute = member.GetCustomAttribute<ScenePersistenceAppendAttribute>(false);
            return attribute == null ? 0 : attribute.Order;
        }

        /// <summary>
        /// Rejects gaps and duplicate append ordinals so future extensions can only be added after the existing suffix.
        /// </summary>
        /// <param name="orderedMembers">Members already grouped into required and appended sections.</param>
        static void ValidateAppendOrders(MemberInfo[] orderedMembers) {
            int expectedOrder = 0;
            for (int index = 0; index < orderedMembers.Length; index++) {
                ScenePersistenceAppendAttribute attribute = orderedMembers[index].GetCustomAttribute<ScenePersistenceAppendAttribute>(false);
                if (attribute == null) {
                    continue;
                }
                if (attribute.Order != expectedOrder) {
                    throw new InvalidOperationException(
                        $"Append-only member '{orderedMembers[index].DeclaringType?.FullName}.{orderedMembers[index].Name}' declares order {attribute.Order}, but the next stable order is {expectedOrder}.");
                }

                expectedOrder++;
            }
        }
    }
}
#endif
