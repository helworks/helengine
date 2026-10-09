using System.Reflection;
using helengine.timeline.runtime;

namespace helengine.timeline {
    /// <summary>
    /// Tools-side catalog of timeline receiver component types, read from their <see cref="TimelineChannelsAttribute"/>.
    /// It resolves a value channel to the one receiver type that declares it; when several types declare the same
    /// channel name, <see cref="BindSlot"/> says which receiver a slot's entity carries.
    /// </summary>
    public sealed class TimelineReceiverCatalog : ITimelineChannelResolver {
        /// <summary>
        /// Registered receiver types in registration order.
        /// </summary>
        readonly List<Type> Types = new List<Type>();

        /// <summary>
        /// Channel declaration of each registered type.
        /// </summary>
        readonly Dictionary<Type, TimelineChannelsAttribute> Declarations = new Dictionary<Type, TimelineChannelsAttribute>();

        /// <summary>
        /// Receiver type chosen for a slot by <see cref="BindSlot"/>.
        /// </summary>
        readonly Dictionary<string, Type> SlotTypes = new Dictionary<string, Type>(StringComparer.Ordinal);

        /// <summary>
        /// Registers a receiver type. It must implement <see cref="ITimelineReceiver"/>, carry
        /// <see cref="TimelineChannelsAttribute"/> with a positive receiver id that no other registered type uses, and
        /// declare each channel once. Registering the same type again does nothing.
        /// </summary>
        /// <param name="receiverType">Component type.</param>
        public void Add(Type receiverType) {
            if (receiverType == null) {
                throw new ArgumentNullException(nameof(receiverType));
            } else if (Declarations.ContainsKey(receiverType)) {
                return;
            } else if (!typeof(ITimelineReceiver).IsAssignableFrom(receiverType)) {
                throw new ArgumentException($"Type '{receiverType.FullName}' does not implement ITimelineReceiver.", nameof(receiverType));
            }

            TimelineChannelsAttribute declaration = receiverType.GetCustomAttribute<TimelineChannelsAttribute>(false);
            if (declaration == null) {
                throw new ArgumentException($"Receiver type '{receiverType.FullName}' has no [TimelineChannels] attribute.", nameof(receiverType));
            } else if (declaration.ReceiverId <= 0) {
                throw new ArgumentException($"Receiver type '{receiverType.FullName}' must declare a positive ReceiverId in [TimelineChannels].", nameof(receiverType));
            }

            for (int index = 0; index < declaration.Channels.Length; index++) {
                if (string.IsNullOrEmpty(declaration.Channels[index]) || Array.IndexOf(declaration.Channels, declaration.Channels[index]) != index) {
                    throw new ArgumentException($"Receiver type '{receiverType.FullName}' declares an empty or repeated channel '{declaration.Channels[index]}'.", nameof(receiverType));
                }
            }
            for (int index = 0; index < Types.Count; index++) {
                if (Declarations[Types[index]].ReceiverId == declaration.ReceiverId) {
                    throw new ArgumentException($"Receiver types '{Types[index].FullName}' and '{receiverType.FullName}' both use receiver id {declaration.ReceiverId}.", nameof(receiverType));
                }
            }

            Types.Add(receiverType);
            Declarations.Add(receiverType, declaration);
        }

        /// <summary>
        /// Registers every receiver type (types carrying <see cref="TimelineChannelsAttribute"/>) of an assembly.
        /// </summary>
        /// <param name="assembly">Assembly to scan, such as the game's script module.</param>
        public void AddAssembly(Assembly assembly) {
            if (assembly == null) {
                throw new ArgumentNullException(nameof(assembly));
            }

            Type[] types = assembly.GetTypes();
            for (int index = 0; index < types.Length; index++) {
                if (types[index].GetCustomAttribute<TimelineChannelsAttribute>(false) != null) {
                    Add(types[index]);
                }
            }
        }

        /// <summary>
        /// States which receiver type the entity bound to a slot carries, to disambiguate channel names that several
        /// receiver types declare.
        /// </summary>
        /// <param name="slot">Root slot name.</param>
        /// <param name="receiverType">Registered receiver type.</param>
        public void BindSlot(string slot, Type receiverType) {
            if (string.IsNullOrEmpty(slot)) {
                throw new ArgumentException("A slot name is required.", nameof(slot));
            } else if (receiverType == null || !Declarations.ContainsKey(receiverType)) {
                throw new ArgumentException("The receiver type must be registered with Add first.", nameof(receiverType));
            }

            SlotTypes[slot] = receiverType;
        }

        /// <summary>
        /// Resolves a channel of a slot to its receiver id and channel index.
        /// </summary>
        /// <param name="slot">Root slot name.</param>
        /// <param name="channel">Channel name.</param>
        /// <returns>The binding.</returns>
        public TimelineChannelBinding Resolve(string slot, string channel) {
            Type bound;
            if (SlotTypes.TryGetValue(slot, out bound)) {
                TimelineChannelsAttribute declaration = Declarations[bound];
                int index = Array.IndexOf(declaration.Channels, channel);
                if (index < 0) {
                    throw new InvalidOperationException($"Slot '{slot}' is bound to receiver '{bound.Name}', which does not declare channel '{channel}' (it declares: {string.Join(", ", declaration.Channels)}).");
                }
                return new TimelineChannelBinding(declaration.ReceiverId, index);
            }

            TimelineChannelBinding found = null;
            Type foundType = null;
            for (int typeIndex = 0; typeIndex < Types.Count; typeIndex++) {
                TimelineChannelsAttribute declaration = Declarations[Types[typeIndex]];
                int index = Array.IndexOf(declaration.Channels, channel);
                if (index < 0) {
                    continue;
                } else if (found != null) {
                    throw new InvalidOperationException($"Channel '{channel}' of slot '{slot}' is declared by receivers '{foundType.Name}' and '{Types[typeIndex].Name}'; call BindSlot to choose one.");
                }

                found = new TimelineChannelBinding(declaration.ReceiverId, index);
                foundType = Types[typeIndex];
            }

            if (found == null) {
                throw new InvalidOperationException($"No registered timeline receiver declares channel '{channel}' (used by slot '{slot}').");
            }
            return found;
        }
    }
}
