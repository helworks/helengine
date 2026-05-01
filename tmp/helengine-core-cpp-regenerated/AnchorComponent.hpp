#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class Component;
class int2;
class Core;
class RenderManager3D;
class float3;
class Entity;
class AnchorData;

#include "Component.hpp"
#include "int2.hpp"
#include "Core.hpp"
#include "Core.hpp"
#include "RenderManager3D.hpp"
#include "Component.hpp"
#include "float3.hpp"
#include "Entity.hpp"
#include "AnchorData.hpp"
#include "runtime/native_nullable.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_list.hpp"
#include "AnchorData.hpp"
#include "Entity.hpp"
#include "runtime/native_nullable.hpp"

class AnchorComponent : public Component
{
public:
    AnchorComponent();

    bool get_IsAnchored();

    void ComponentRemoved(::Entity* entity);

    void DisableAnchoring();

    void EnableAnchoring(bool left, bool right, bool top, bool bottom);

    std::string GetAnchorInfo();

    void SetAnchorDistances(Nullable<float> left, Nullable<float> right, Nullable<float> top, Nullable<float> bottom);

    ::Entity* get_Parent();

    void set_Parent(::Entity* value);
private:
    ::AnchorData* anchorData;

    void OnWindowResized(intptr_t handle, int32_t newWidth, int32_t newHeight);
};
