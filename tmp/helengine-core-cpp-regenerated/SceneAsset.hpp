#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class Asset;
class SceneAssetReference;
class SceneEntityAsset;

#include "Asset.hpp"
#include "runtime/native_string.hpp"
#include "runtime/array.hpp"
#include "SceneAssetReference.hpp"
#include "runtime/array.hpp"
#include "SceneEntityAsset.hpp"

class SceneAsset : public Asset
{
public:
    virtual ~SceneAsset() = default;

    SceneAsset();

    static std::string FileExtension;

    Array<::SceneAssetReference*>* AssetReferences;

    Array<::SceneAssetReference*>* get_AssetReferences();
    void set_AssetReferences(Array<::SceneAssetReference*>* value);

    Array<::SceneEntityAsset*>* RootEntities;

    Array<::SceneEntityAsset*>* get_RootEntities();
    void set_RootEntities(Array<::SceneEntityAsset*>* value);

    std::string get_Id();

    void set_Id(std::string value);
};
