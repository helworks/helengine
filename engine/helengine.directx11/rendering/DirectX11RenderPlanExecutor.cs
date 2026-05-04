namespace helengine.directx11 {
    /// <summary>
    /// Executes one ordered DirectX11 render plan by dispatching pass kinds to a concrete pass executor.
    /// </summary>
    public sealed class DirectX11RenderPlanExecutor {
        /// <summary>
        /// Stores whether shadow passes should execute in the current runtime slice.
        /// </summary>
        bool ExecuteShadowPassesValue;
        /// <summary>
        /// Stores whether post-process passes should execute in the current runtime slice.
        /// </summary>
        bool ExecutePostProcessPassesValue;

        /// <summary>
        /// Initializes one plan executor using the current first-slice execution policy.
        /// </summary>
        public DirectX11RenderPlanExecutor() : this(false, false) {
        }

        /// <summary>
        /// Initializes one plan executor with explicit pass-execution policy flags.
        /// </summary>
        /// <param name="executeShadowPasses">Whether shadow passes should execute instead of being skipped.</param>
        /// <param name="executePostProcessPasses">Whether post-process passes should execute instead of being skipped.</param>
        public DirectX11RenderPlanExecutor(bool executeShadowPasses, bool executePostProcessPasses) {
            ExecuteShadowPassesValue = executeShadowPasses;
            ExecutePostProcessPassesValue = executePostProcessPasses;
        }

        /// <summary>
        /// Executes one ordered render plan for the supplied frame context.
        /// </summary>
        /// <param name="context">Execution context containing the frame and output surface.</param>
        /// <param name="plan">Ordered pass list to execute.</param>
        /// <param name="passExecutor">Concrete pass executor that performs backend-specific work.</param>
        public void ExecutePlan(
            DirectX11RenderPassExecutionContext context,
            RenderPlan plan,
            IDirectX11RenderPassExecutor passExecutor) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            } else if (plan == null) {
                throw new ArgumentNullException(nameof(plan));
            } else if (passExecutor == null) {
                throw new ArgumentNullException(nameof(passExecutor));
            }

            for (int passIndex = 0; passIndex < plan.Passes.Count; passIndex++) {
                RenderPassKind passKind = plan.Passes[passIndex];
                ExecutePass(passKind, context, passExecutor);
            }
        }

        /// <summary>
        /// Executes one planned pass kind according to the current runtime slice policy.
        /// </summary>
        /// <param name="passKind">Pass kind selected by the shared render plan.</param>
        /// <param name="context">Execution context containing the frame and output surface.</param>
        /// <param name="passExecutor">Concrete pass executor that performs backend-specific work.</param>
        void ExecutePass(
            RenderPassKind passKind,
            DirectX11RenderPassExecutionContext context,
            IDirectX11RenderPassExecutor passExecutor) {
            if (passKind == RenderPassKind.DepthPrepass) {
                passExecutor.ExecuteDepthPrepass(context);
            } else if (passKind == RenderPassKind.Shadow) {
                if (ExecuteShadowPassesValue) {
                    passExecutor.ExecuteShadowPass(context);
                }
            } else if (passKind == RenderPassKind.OpaqueForward) {
                passExecutor.ExecuteOpaqueForwardPass(context);
            } else if (passKind == RenderPassKind.TransparentForward) {
                passExecutor.ExecuteTransparentForwardPass(context);
            } else if (passKind == RenderPassKind.PostProcess) {
                if (ExecutePostProcessPassesValue) {
                    passExecutor.ExecutePostProcessPass(context);
                }
            } else if (passKind == RenderPassKind.Present) {
                passExecutor.ExecutePresentPass(context);
            } else {
                throw new InvalidOperationException($"Unsupported render pass kind '{passKind}'.");
            }
        }
    }
}
