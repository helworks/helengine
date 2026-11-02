#pragma once
#include <memory>

namespace helengine::vulkan {
/// <summary>Releases fully constructed generated value containers whose Dispose frees owned memory and whose generated destructor alone is intentionally empty; Core callback shutdown remains explicit.</summary>
template<class T> class GeneratedDisposableDeleter final {
public:
    /// <summary>Disposes the generated object's owned buffers once before deleting its wrapper.</summary>
    void operator()(T* value) const noexcept { if (value) { value->Dispose(); delete value; } }
};
/// <summary>Combines normal unique ownership with the generated container's explicit memory-disposal contract.</summary>
template<class T> using GeneratedDisposableOwner=std::unique_ptr<T,GeneratedDisposableDeleter<T>>;
}
