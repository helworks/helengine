#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class SceneComponentAssetRecord;
class float4;
class float3;

#include "runtime/array.hpp"
#include "runtime/array.hpp"
#include "SceneComponentAssetRecord.hpp"
#include "float4.hpp"
#include "float3.hpp"
#include "runtime/native_string.hpp"

class SceneEntityAsset
{
public:
    SceneEntityAsset();

    Array<::SceneEntityAsset*>* Children;

    Array<::SceneEntityAsset*>* get_Children();
    void set_Children(Array<::SceneEntityAsset*>* value);

    Array<::SceneComponentAssetRecord*>* Components;

    Array<::SceneComponentAssetRecord*>* get_Components();
    void set_Components(Array<::SceneComponentAssetRecord*>* value);

    ::float4 LocalOrientation;

    ::float4 get_LocalOrientation();
    void set_LocalOrientation(::float4 value);

    ::float3 LocalPosition;

    ::float3 get_LocalPosition();
    void set_LocalPosition(::float3 value);

    ::float3 LocalScale;

    ::float3 get_LocalScale();
    void set_LocalScale(::float3 value);

    std::string Name;

    std::string get_Name();
    void set_Name(std::string value);
};
