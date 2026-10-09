using helengine.timeline.runtime;

namespace helengine.timeline.tests {
    /// <summary>
    /// Verifies how the tools turn <c>[TimelineChannels]</c> declarations into receiver ids and channel indices.
    /// </summary>
    public class TimelineReceiverCatalogTests {
        /// <summary>
        /// A channel declared by one receiver resolves to that receiver's id and the channel's position.
        /// </summary>
        [Fact]
        public void Resolve_uniqueChannel_returnsReceiverAndIndex() {
            TimelineReceiverCatalog catalog = new TimelineReceiverCatalog();
            catalog.Add(typeof(TextEffectReceiver));
            catalog.Add(typeof(LightReceiver));

            TimelineChannelBinding binding = catalog.Resolve("lamp", "range");

            Assert.Equal(LightReceiver.Id, binding.ReceiverId);
            Assert.Equal(1, binding.ChannelIndex);
        }

        /// <summary>
        /// A channel two receivers declare is ambiguous until the slot is bound to one of them.
        /// </summary>
        [Fact]
        public void Resolve_ambiguousChannel_needsSlotBinding() {
            TimelineReceiverCatalog catalog = new TimelineReceiverCatalog();
            catalog.AddAssembly(typeof(TextEffectReceiver).Assembly);

            InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() => catalog.Resolve("lamp", "opacity"));
            Assert.Contains("BindSlot", failure.Message);

            catalog.BindSlot("lamp", typeof(LightReceiver));
            TimelineChannelBinding binding = catalog.Resolve("lamp", "opacity");
            Assert.Equal(LightReceiver.Id, binding.ReceiverId);
            Assert.Equal(2, binding.ChannelIndex);
        }

        /// <summary>
        /// Unknown channels and types without the attribute are rejected with readable messages.
        /// </summary>
        [Fact]
        public void UnknownChannelAndUndeclaredTypes_areRejected() {
            TimelineReceiverCatalog catalog = new TimelineReceiverCatalog();
            catalog.Add(typeof(TextEffectReceiver));

            Assert.Contains("'fov'", Assert.Throws<InvalidOperationException>(() => catalog.Resolve("camera", "fov")).Message);
            Assert.Throws<ArgumentException>(() => catalog.Add(typeof(TimelineReceiverCatalogTests)));
        }
    }
}
