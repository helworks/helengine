#pragma once

#include <type_traits>

/// <summary>
/// Provides the lightweight cast helper used by transpiled declaration-pattern lowering.
/// </summary>
template <typename TTarget, typename TSource>
inline auto he_cpp_try_cast(TSource* source)
    -> std::conditional_t<std::is_pointer_v<TTarget>, TTarget, TTarget*> {
    if (source == nullptr) {
        return nullptr;
    }

    if constexpr (std::is_pointer_v<TTarget>) {
        using target_value_type = std::remove_pointer_t<TTarget>;
        return static_cast<TTarget>(static_cast<target_value_type*>(source));
    } else {
        return static_cast<TTarget*>(source);
    }
}
