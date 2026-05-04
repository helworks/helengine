#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class Component;
class float3;
class Entity;
class AnchorData;
class IAnchorBoundsProvider;
class IAnchorSizeProvider;

#include "Component.hpp"
#include "runtime/native_exceptions.hpp"
#include "int2.hpp"
#include "float3.hpp"
#include "Entity.hpp"
#include "AnchorData.hpp"
#include "runtime/native_nullable.hpp"
#include "Component.hpp"
#include "IAnchorBoundsProvider.hpp"
#include "Entity.hpp"
#include "IAnchorBoundsProvider.hpp"
#include "int2.hpp"
#include "IAnchorSizeProvider.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_list.hpp"
#include "AnchorData.hpp"
#include "runtime/native_nullable.hpp"

class AnchorComponent : public Component
{
public:
    virtual ~AnchorComponent() = default;

    AnchorComponent();

    bool get_IsAnchored();

    void ComponentAdded(::Entity* entity);

    void ComponentRemoved(::Entity* entity);

    void DisableAnchoring();

    void EnableAnchoring(bool left, bool right, bool top, bool bottom);

    std::string GetAnchorInfo();

    void RefreshAnchoring();

    void SetAnchorDistances(Nullable<float> left, Nullable<float> right, Nullable<float> top, Nullable<float> bottom);

    ::Entity* get_Parent();

    void set_Parent(::Entity* value);
private:
    bool IsSubscribedToWindowResize;

    ::IAnchorBoundsProvider* anchorBoundsProvider;

    ::AnchorData* anchorData;

    void AttachToWindowResize();

    void DetachFromBoundsProvider();

    void DetachFromWindowResize();

    int32_t GetAnchorArea(int2* size);

    int2* GetAnchorBounds();

    int2* GetAnchorSize();

    void HandleAnchorBoundsChanged();

    void HandleWindowResized(intptr_t handle, int32_t newWidth, int32_t newHeight);

    void RefreshSubscriptions();

    ::IAnchorBoundsProvider* ResolveAnchorBoundsProvider();
};
