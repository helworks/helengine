namespace helengine.core.tests.host {
    /// <summary>
    /// Verifies that host messages are queued until the frame dispatch, delivered in order exactly once, and that game
    /// messages reach the platform channel unchanged.
    /// </summary>
    public sealed class HostMessageManagerTests {
        /// <summary>
        /// Received messages wait for <see cref="HostMessageManager.Dispatch"/> and then arrive in order, once.
        /// </summary>
        [Fact]
        public void Receive_isDeliveredOnDispatchInOrderOnce() {
            HostMessageManager manager = new HostMessageManager(new RecordingHostMessageBackend());
            List<string> seen = new List<string>();
            manager.MessageReceived += (channel, payload) => seen.Add(channel + "=" + payload);

            manager.Receive("time", "1.5");
            manager.Receive("focus", "2");
            Assert.Empty(seen);
            Assert.Equal(2, manager.PendingCount);

            manager.Dispatch();
            manager.Dispatch();

            Assert.Equal(new[] { "time=1.5", "focus=2" }, seen);
            Assert.Equal(0, manager.PendingCount);
        }

        /// <summary>
        /// A message posted while handlers run is kept for the next frame instead of being raised re-entrantly.
        /// </summary>
        [Fact]
        public void MessageReceivedDuringDispatch_waitsForNextDispatch() {
            HostMessageManager manager = new HostMessageManager(new RecordingHostMessageBackend());
            List<string> seen = new List<string>();
            manager.MessageReceived += (channel, payload) => {
                seen.Add(channel);
                if (channel == "first") {
                    manager.Receive("second", string.Empty);
                }
            };

            manager.Receive("first", string.Empty);
            manager.Dispatch();
            Assert.Equal(new[] { "first" }, seen);

            manager.Dispatch();
            Assert.Equal(new[] { "first", "second" }, seen);
        }

        /// <summary>
        /// Sent messages reach the platform channel unchanged.
        /// </summary>
        [Fact]
        public void Send_forwardsToBackend() {
            RecordingHostMessageBackend backend = new RecordingHostMessageBackend();
            new HostMessageManager(backend).Send("labels", "[1,2]");

            Assert.Equal("labels", Assert.Single(backend.Channels));
            Assert.Equal("[1,2]", Assert.Single(backend.Payloads));
        }

        /// <summary>
        /// Messages need a routing name and a non-null body in both directions.
        /// </summary>
        [Fact]
        public void InvalidMessages_throw() {
            HostMessageManager manager = new HostMessageManager(new RecordingHostMessageBackend());

            Assert.Throws<ArgumentException>(() => manager.Receive(string.Empty, "x"));
            Assert.Throws<ArgumentNullException>(() => manager.Receive("time", null));
            Assert.Throws<ArgumentException>(() => manager.Send(null, "x"));
            Assert.Throws<ArgumentNullException>(() => manager.Send("time", null));
        }

        /// <summary>
        /// The core exposes no host channel until a platform attaches one.
        /// </summary>
        [Fact]
        public void Core_hasHostMessagesOnlyAfterBackendIsAttached() {
            Core core = new Core();
            Assert.Null(core.HostMessages);
            Assert.Throws<ArgumentNullException>(() => core.SetHostMessageBackend(null));

            core.SetHostMessageBackend(new RecordingHostMessageBackend());
            Assert.NotNull(core.HostMessages);
        }
    }
}
