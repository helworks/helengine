#pragma once

#include <utility>

/// <summary>
/// Executes a captured callback when the surrounding scope exits.
/// </summary>
/// <typeparam name="TCallback">Callable shape used for the scope-exit action.</typeparam>
template <typename TCallback>
class he_cpp_scope_exit {
public:
    /// <summary>
    /// Captures the callback that should run when the guard is destroyed.
    /// </summary>
    /// <param name="callback">Callback invoked on scope exit while the guard remains active.</param>
    explicit he_cpp_scope_exit(TCallback callback)
        : callback_(std::move(callback)), active_(true) {
    }

    he_cpp_scope_exit(const he_cpp_scope_exit&) = delete;
    he_cpp_scope_exit& operator=(const he_cpp_scope_exit&) = delete;

    /// <summary>
    /// Transfers ownership of an active scope-exit callback to a new guard instance.
    /// </summary>
    /// <param name="other">Guard transferring callback ownership.</param>
    he_cpp_scope_exit(he_cpp_scope_exit&& other) noexcept
        : callback_(std::move(other.callback_)), active_(other.active_) {
        other.active_ = false;
    }

    /// <summary>
    /// Runs the captured callback exactly once when the guard leaves scope.
    /// </summary>
    ~he_cpp_scope_exit() {
        if (active_) {
            callback_();
        }
    }

private:
    TCallback callback_;
    bool active_;
};

/// <summary>
/// Creates a scope-exit guard for a callback captured at the call site.
/// </summary>
/// <typeparam name="TCallback">Callable shape used for the scope-exit action.</typeparam>
/// <param name="callback">Callback invoked when the returned guard is destroyed.</param>
/// <returns>A movable scope-exit guard that owns the callback.</returns>
template <typename TCallback>
he_cpp_scope_exit<TCallback> he_cpp_make_scope_exit(TCallback callback) {
    return he_cpp_scope_exit<TCallback>(std::move(callback));
}
