#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

#include "runtime/native_string.hpp"
#include "SceneAssetReferenceSourceKind.hpp"

class SceneAssetReference
{
public:
    SceneAssetReference();

    std::string AssetId;

    std::string get_AssetId();
    void set_AssetId(std::string value);

    std::string ProviderId;

    std::string get_ProviderId();
    void set_ProviderId(std::string value);

    std::string RelativePath;

    std::string get_RelativePath();
    void set_RelativePath(std::string value);

    ::SceneAssetReferenceSourceKind SourceKind;

    ::SceneAssetReferenceSourceKind get_SourceKind();
    void set_SourceKind(::SceneAssetReferenceSourceKind value);
};
