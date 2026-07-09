using System.Collections.Generic;

namespace helengine {
    /// <summary>
    /// Observes trigger events for the owning entity against one authored target scene entity.
    /// </summary>
    public sealed class SceneEntityTriggerObserverComponent : UpdateComponent {
        /// <summary>
        /// Gets or sets whether trigger-state updates should be skipped for the current frame.
        /// </summary>
        public bool UpdatesAreSuppressed { get; set; }

        /// <summary>
        /// Gets or sets the authored scene entity that should be matched as the other trigger participant.
        /// </summary>
        public SceneEntityReference TargetEntityReference { get; set; }

        /// <summary>
        /// Cached live runtime target entity resolved from the serialized target reference.
        /// </summary>
        Entity TargetEntity;

        /// <summary>
        /// Tracks whether the target entity is currently overlapping the owning trigger.
        /// </summary>
        bool IsTriggeredValue;

        /// <summary>
        /// Tracks whether the target entity entered the owning trigger during the current frame.
        /// </summary>
        bool WasEnteredThisFrameValue;

        /// <summary>
        /// Tracks whether the target entity exited the owning trigger during the current frame.
        /// </summary>
        bool WasExitedThisFrameValue;

        /// <summary>
        /// Initializes one reusable trigger observer.
        /// </summary>
        public SceneEntityTriggerObserverComponent() {
            UpdateOrder = 0;
        }

        /// <summary>
        /// Gets whether the target entity is currently overlapping the owning trigger.
        /// </summary>
        /// <returns>True when the target entity is inside the owning trigger; otherwise false.</returns>
        public bool GetIsTriggered() {
            return IsTriggeredValue;
        }

        /// <summary>
        /// Gets whether the target entity entered the owning trigger during the current frame.
        /// </summary>
        /// <returns>True when a matching enter event was observed this frame; otherwise false.</returns>
        public bool GetWasEnteredThisFrame() {
            return WasEnteredThisFrameValue;
        }

        /// <summary>
        /// Gets whether the target entity exited the owning trigger during the current frame.
        /// </summary>
        /// <returns>True when a matching exit event was observed this frame; otherwise false.</returns>
        public bool GetWasExitedThisFrame() {
            return WasExitedThisFrameValue;
        }

        /// <summary>
        /// Consumes the latest physics trigger events and updates the current overlap state for the owning entity.
        /// </summary>
        public override void Update() {
            base.Update();

            if (Parent == null) {
                throw new InvalidOperationException("SceneEntityTriggerObserverComponent requires an attached trigger entity.");
            }

            WasEnteredThisFrameValue = false;
            WasExitedThisFrameValue = false;
            if (UpdatesAreSuppressed) {
                return;
            }

            ResolveTargetEntityWhenNeeded();
            IPhysicsTriggerEventRuntime3D triggerRuntime = ResolveRequiredTriggerRuntime();
            ResolveObservedState(
                triggerRuntime.TriggerEvents,
                Parent,
                TargetEntity,
                IsTriggeredValue,
                out IsTriggeredValue,
                out WasEnteredThisFrameValue,
                out WasExitedThisFrameValue);
        }

        /// <summary>
        /// Resolves the overlap state produced by one frame of trigger events for one trigger-target pair.
        /// </summary>
        /// <param name="triggerEvents">Frame trigger events emitted by the physics runtime.</param>
        /// <param name="triggerEntity">Entity that owns the observed trigger collider.</param>
        /// <param name="targetEntity">Entity that should match the other trigger participant.</param>
        /// <param name="previousIsTriggered">Previous persistent overlap state.</param>
        /// <param name="isTriggered">Resolved persistent overlap state after all matching events are processed.</param>
        /// <param name="wasEnteredThisFrame">True when a matching enter event occurred this frame.</param>
        /// <param name="wasExitedThisFrame">True when a matching exit event occurred this frame.</param>
        public static void ResolveObservedState(
            IReadOnlyList<TriggerEvent3D> triggerEvents,
            Entity triggerEntity,
            Entity targetEntity,
            bool previousIsTriggered,
            out bool isTriggered,
            out bool wasEnteredThisFrame,
            out bool wasExitedThisFrame) {
            if (triggerEvents == null) {
                throw new ArgumentNullException(nameof(triggerEvents));
            } else if (triggerEntity == null) {
                throw new ArgumentNullException(nameof(triggerEntity));
            } else if (targetEntity == null) {
                throw new ArgumentNullException(nameof(targetEntity));
            }

            isTriggered = previousIsTriggered;
            wasEnteredThisFrame = false;
            wasExitedThisFrame = false;

            for (int eventIndex = 0; eventIndex < triggerEvents.Count; eventIndex++) {
                TriggerEvent3D triggerEvent = triggerEvents[eventIndex];
                if (!ReferenceEquals(triggerEvent.TriggerEntity, triggerEntity)
                    || !ReferenceEquals(triggerEvent.OtherEntity, targetEntity)) {
                    continue;
                }

                if (triggerEvent.Kind == TriggerEventKind3D.Enter) {
                    isTriggered = true;
                    wasEnteredThisFrame = true;
                    wasExitedThisFrame = false;
                } else if (triggerEvent.Kind == TriggerEventKind3D.Stay) {
                    isTriggered = true;
                } else if (triggerEvent.Kind == TriggerEventKind3D.Exit) {
                    isTriggered = false;
                    wasEnteredThisFrame = false;
                    wasExitedThisFrame = true;
                }
            }
        }

        /// <summary>
        /// Resolves the active physics runtime that publishes trigger events.
        /// </summary>
        /// <returns>Trigger-event runtime exposed by the active physics backend.</returns>
        IPhysicsTriggerEventRuntime3D ResolveRequiredTriggerRuntime() {
            if (Core.Instance == null) {
                throw new InvalidOperationException("A core instance must exist before gameplay trigger observation can run.");
            }

            IPhysicsTriggerEventRuntime3D triggerRuntime = Core.Instance.PhysicsRuntime as IPhysicsTriggerEventRuntime3D;
            if (triggerRuntime == null) {
                throw new InvalidOperationException("SceneEntityTriggerObserverComponent requires a physics runtime that supports trigger events.");
            }

            return triggerRuntime;
        }

        /// <summary>
        /// Resolves the tracked target entity from the serialized scene reference when the runtime cache is still empty.
        /// </summary>
        void ResolveTargetEntityWhenNeeded() {
            if (TargetEntity != null) {
                return;
            } else if (TargetEntityReference == null) {
                throw new InvalidOperationException("SceneEntityTriggerObserverComponent requires a serialized target entity reference.");
            } else if (TargetEntityReference.EntityId == 0u) {
                throw new InvalidOperationException("SceneEntityTriggerObserverComponent requires a non-zero target scene entity id.");
            } else if (Core.Instance == null) {
                throw new InvalidOperationException("A core instance must exist before gameplay trigger observation can resolve scene references.");
            }

            List<Entity> entities = Core.Instance.ObjectManager.Entities;
            for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++) {
                Entity match = TryFindEntityBySceneIdRecursive(entities[entityIndex], TargetEntityReference.EntityId);
                if (match != null) {
                    TargetEntity = match;
                    return;
                }
            }

            throw new InvalidOperationException($"SceneEntityTriggerObserverComponent could not resolve target scene entity id {TargetEntityReference.EntityId}.");
        }

        /// <summary>
        /// Recursively finds one runtime entity with the supplied authored scene id.
        /// </summary>
        /// <param name="entity">Current runtime subtree root.</param>
        /// <param name="sceneEntityId">Authored scene entity id to search for.</param>
        /// <returns>Matching entity, or <c>null</c> when absent from the subtree.</returns>
        static Entity TryFindEntityBySceneIdRecursive(Entity entity, uint sceneEntityId) {
            if (entity == null) {
                return null;
            }

            if (FindSceneEntityRuntimeIdOrZero(entity) == sceneEntityId) {
                return entity;
            }
            if (entity.Children == null) {
                return null;
            }

            for (int childIndex = 0; childIndex < entity.Children.Count; childIndex++) {
                Entity match = TryFindEntityBySceneIdRecursive(entity.Children[childIndex], sceneEntityId);
                if (match != null) {
                    return match;
                }
            }

            return null;
        }

        /// <summary>
        /// Resolves the authored scene entity id attached to one runtime entity when present.
        /// </summary>
        /// <param name="entity">Runtime entity to inspect.</param>
        /// <returns>Authored scene entity id when present; otherwise <c>0</c>.</returns>
        static uint FindSceneEntityRuntimeIdOrZero(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            } else if (entity.Components == null) {
                return 0u;
            }

            for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
                if (entity.Components[componentIndex] is SceneEntityRuntimeIdComponent runtimeIdComponent) {
                    return runtimeIdComponent.SceneEntityId;
                }
            }

            return 0u;
        }
    }
}
