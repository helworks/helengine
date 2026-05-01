#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class Asset;
class SceneEntityAsset;

#include "Asset.hpp"
#include "runtime/native_string.hpp"
#include "runtime/array.hpp"
#include "SceneEntityAsset.hpp"

class SceneAsset : public Asset
{
public:
    SceneAsset();

    static std::string FileExtension;

    Array<::SceneEntityAsset*>* RootEntities;

    Array<::SceneEntityAsset*>* get_RootEntities();
    void set_RootEntities(Array<::SceneEntityAsset*>* value);

    std::string get_Id();

    void set_Id(std::string value);
};
