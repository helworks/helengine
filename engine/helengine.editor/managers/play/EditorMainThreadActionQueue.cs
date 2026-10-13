namespace helengine.editor {
    /// <summary>
    /// Marshals work from background threads onto the editor frame thread. Background producers, such as the player
    /// process's console and exit callbacks, enqueue actions from any thread; the editor session drains the queue once per
    /// frame, so every queued action runs on the frame thread in the order it was enqueued. This follows the same
    /// queue-then-drain pattern as <see cref="EditorShaderBuildNotificationQueue"/>.
    /// </summary>
    public sealed class EditorMainThreadActionQueue {
        /// <summary>
        /// Initial capacity reserved for pending actions.
        /// </summary>
        const int PendingActionInitialCapacity = 16;

        /// <summary>
        /// Guards the pending-action queue against concurrent producer and frame-thread access.
        /// </summary>
        readonly object PendingActionLock = new object();

        /// <summary>
        /// Actions waiting to run on the frame thread, in enqueue order.
        /// </summary>
        readonly Queue<Action> PendingActions = new Queue<Action>(PendingActionInitialCapacity);

        /// <summary>
        /// Queues one action to run on the frame thread during the next drain. Safe to call from any thread.
        /// </summary>
        /// <param name="action">Work to run on the frame thread.</param>
        /// <exception cref="ArgumentNullException">Thrown when the action is null.</exception>
        public void Enqueue(Action action) {
            if (action == null) {
                throw new ArgumentNullException(nameof(action));
            }

            lock (PendingActionLock) {
                PendingActions.Enqueue(action);
            }
        }

        /// <summary>
        /// Runs every queued action on the calling thread, which must be the editor frame thread. Actions enqueued while
        /// the drain runs are also executed before it returns. An action that throws stops the drain and propagates; the
        /// actions behind it stay queued for the next drain.
        /// </summary>
        public void ProcessPending() {
            while (true) {
                Action action;
                lock (PendingActionLock) {
                    if (PendingActions.Count == 0) {
                        return;
                    }

                    action = PendingActions.Dequeue();
                }

                action();
            }
        }
    }
}
