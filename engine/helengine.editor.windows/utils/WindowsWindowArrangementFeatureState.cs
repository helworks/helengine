using System.Runtime.InteropServices;

namespace helengine.editor.windows {
    /// <summary>
    /// Reads the current Windows window-arrangement feature flags from User32 system parameters.
    /// </summary>
    public sealed class WindowsWindowArrangementFeatureState : IWindowArrangementFeatureState {
        /// <summary>
        /// System parameter that reports whether window arrangement is enabled globally.
        /// </summary>
        const uint SpiGetWinArranging = 0x0082;
        /// <summary>
        /// System parameter that reports whether dragging a maximized title bar restores the window.
        /// </summary>
        const uint SpiGetDragFromMaximize = 0x008C;
        /// <summary>
        /// System parameter that reports whether dragging a window to a screen edge docks or maximizes it.
        /// </summary>
        const uint SpiGetDockMoving = 0x0090;
        /// <summary>
        /// Cached value indicating whether Windows window arrangement is enabled globally.
        /// </summary>
        readonly bool IsWindowArrangingEnabledValue;
        /// <summary>
        /// Cached value indicating whether dragging a window to a screen edge should dock or maximize it.
        /// </summary>
        readonly bool IsDockMovingEnabledValue;
        /// <summary>
        /// Cached value indicating whether dragging a maximized title bar should restore the window.
        /// </summary>
        readonly bool IsDragFromMaximizeEnabledValue;

        /// <summary>
        /// Reads and caches the Windows arrangement feature flags for the lifetime of this state provider.
        /// </summary>
        public WindowsWindowArrangementFeatureState() {
            IsWindowArrangingEnabledValue = GetWindowArrangementFeatureValue(SpiGetWinArranging, nameof(IsWindowArrangingEnabled));
            IsDockMovingEnabledValue = GetWindowArrangementFeatureValue(SpiGetDockMoving, nameof(IsDockMovingEnabled));
            IsDragFromMaximizeEnabledValue = GetWindowArrangementFeatureValue(SpiGetDragFromMaximize, nameof(IsDragFromMaximizeEnabled));
        }

        /// <summary>
        /// Gets a value indicating whether Windows window arrangement is enabled globally.
        /// </summary>
        public bool IsWindowArrangingEnabled => IsWindowArrangingEnabledValue;

        /// <summary>
        /// Gets a value indicating whether dragging a window to a screen edge should dock or maximize it.
        /// </summary>
        public bool IsDockMovingEnabled => IsDockMovingEnabledValue;

        /// <summary>
        /// Gets a value indicating whether dragging a maximized title bar should restore the window.
        /// </summary>
        public bool IsDragFromMaximizeEnabled => IsDragFromMaximizeEnabledValue;

        /// <summary>
        /// Retrieves a Windows arrangement feature flag from User32.
        /// </summary>
        /// <param name="action">System parameter action that identifies the feature flag.</param>
        /// <param name="featureName">Name of the feature being queried for diagnostics.</param>
        /// <returns>True when the queried feature is enabled.</returns>
        static bool GetWindowArrangementFeatureValue(uint action, string featureName) {
            if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1)) {
                return false;
            }

            bool value = false;
            if (SystemParametersInfo(action, 0, ref value, 0)) {
                return value;
            }

            int lastError = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"Failed to query Windows arrangement feature '{featureName}' (Win32 error {lastError}).");
        }

        /// <summary>
        /// Retrieves a system-wide Windows parameter from User32.
        /// </summary>
        /// <param name="uiAction">System parameter action to query.</param>
        /// <param name="uiParam">Additional query parameter.</param>
        /// <param name="pvParam">Output value populated by User32.</param>
        /// <param name="fWinIni">Additional flags that control persistence or notifications.</param>
        /// <returns>True when the query succeeds.</returns>
        [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", ExactSpelling = true, SetLastError = true)]
        static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref bool pvParam, uint fWinIni);
    }
}
