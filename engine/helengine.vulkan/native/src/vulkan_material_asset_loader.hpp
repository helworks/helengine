#pragma once
#include "generated_disposable_deleter.hpp"
#include "Asset.hpp"
#include "EngineBinaryReadContext.hpp"
#include "IContentStreamSource.hpp"
#include "runtime/finally.hpp"
#include "runtime/native_disposable.hpp"
#include "system/io/stream.hpp"
#include <stdexcept>

namespace helengine::vulkan {
/// <summary>Reads independently owned native material inputs through public SDK serializers without depending on generated out-of-line content-manager templates.</summary>
class VulkanMaterialAssetLoader final {
public:
    /// <summary>Preserves SDK read diagnostics, disposes the borrowed source's newly opened stream, validates the decoded type and transfers its buffers to one explicit asset owner.</summary>
    template<class TAsset,class TDecoder> static GeneratedDisposableOwner<TAsset> Read(::IContentStreamSource* source,HeCppString path,TDecoder decode) {
        if (!source) { throw std::invalid_argument("Linux cooked material assets require a content stream source."); }
        const auto previousPath=::EngineBinaryReadContext::get_CurrentAssetPath();
        const auto previousStage=::EngineBinaryReadContext::get_CurrentReadStage();
        auto restore=he_cpp_make_scope_exit([&] {
            ::EngineBinaryReadContext::set_CurrentAssetPath(previousPath);
            ::EngineBinaryReadContext::set_CurrentReadStage(previousStage);
        });
        ::EngineBinaryReadContext::set_CurrentAssetPath(path);
        ::EngineBinaryReadContext::set_CurrentReadStage("ContentManager:OpenRead");
        GeneratedDisposableOwner<::Stream> stream(source->OpenRead(path));
        if (!stream) { throw std::runtime_error("Linux cooked material asset stream is missing: "+path.raw()); }
        ::EngineBinaryReadContext::set_CurrentReadStage("ContentManager:ProcessorRead");
        std::unique_ptr<::Asset> asset(decode(stream.get()));
        auto dispose=he_cpp_make_scope_exit([&] {
            if (auto* disposable=dynamic_cast<::IDisposable*>(asset.get())) { disposable->Dispose(); }
        });
        auto* typed=dynamic_cast<TAsset*>(asset.get());
        if (!typed) { throw std::runtime_error("Linux cooked material input has an unexpected asset type: "+path.raw()); }
        asset.release();
        return GeneratedDisposableOwner<TAsset>(typed);
    }
};
}
