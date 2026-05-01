#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class Component;
class float4;
class float3;

#include "runtime/native_disposable.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_list.hpp"
#include "Component.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_list.hpp"
#include "Component.hpp"
#include "float4.hpp"
#include "float3.hpp"

class Entity : public IDisposable
{
public:
    List<::Entity*>* Children;

    List<::Entity*>* get_Children();
    void set_Children(List<::Entity*>* value);

    List<::Component*>* Components;

    List<::Component*>* get_Components();
    void set_Components(List<::Component*>* value);

    bool get_Enabled();

    void set_Enabled(bool value);

    bool get_IsHierarchyEnabled();

    uint16_t LayerMask;

    uint16_t get_LayerMask();
    void set_LayerMask(uint16_t value);

    ::float4 get_LocalOrientation();

    void set_LocalOrientation(::float4 value);

    ::float3 get_LocalPosition();

    void set_LocalPosition(::float3 value);

    ::float3 get_LocalScale();

    void set_LocalScale(::float3 value);

    ::float4 get_Orientation();

    void set_Orientation(::float4 value);

    ::Entity* Parent;

    ::Entity* get_Parent();
    void set_Parent(::Entity* value);

    virtual ::float3 get_Position();

    virtual void set_Position(::float3 value);

    ::float3 get_Scale();

    void set_Scale(::float3 value);

    bool get_Static();

    void set_Static(bool value);

    void AddChild(::Entity* entity);

    void AddComponent(::Component* comp);

    void Dispose();

    Entity();

    void InitChildren();

    void InitComponents();

    void RemoveChild(::Entity* entity);

    void RemoveComponent(::Component* comp);
protected:
    virtual void ParentEnabledChange(bool newEnabled);

    virtual void ParentStaticChange(bool newEnabled);
private:
    bool isEnabled;

    bool isStatic;

    ::float4 orientation;

    ::float3 position;

    ::float3 scale;
};
