#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class Component;
class IRoundedRectDrawable2D;
class IDrawable2D;
class Entity;
class Core;
class ObjectManager;
class RenderManager2D;
class byte4;
class int2;
class float4;

#include "Component.hpp"
#include "IRoundedRectDrawable2D.hpp"
#include "IDrawable2D.hpp"
#include "Component.hpp"
#include "Entity.hpp"
#include "Core.hpp"
#include "Core.hpp"
#include "ObjectManager.hpp"
#include "RenderManager2D.hpp"
#include "byte4.hpp"
#include "int2.hpp"
#include "float4.hpp"
#include "Entity.hpp"

class RoundedRectComponent : public Component, public IRoundedRectDrawable2D
{
public:
    ::byte4 BorderColor;

    ::byte4 get_BorderColor();
    void set_BorderColor(::byte4 value);

    float BorderThickness;

    float get_BorderThickness();
    void set_BorderThickness(float value);

    ::byte4 Color;

    ::byte4 get_Color();
    void set_Color(::byte4 value);

    ::byte4 FillColor;

    ::byte4 get_FillColor();
    void set_FillColor(::byte4 value);

    uint8_t LayerMask;

    uint8_t get_LayerMask();
    void set_LayerMask(uint8_t value);

    float Radius;

    float get_Radius();
    void set_Radius(float value);

    uint8_t get_RenderOrder2D();

    void set_RenderOrder2D(uint8_t value);

    float Rotation;

    float get_Rotation();
    void set_Rotation(float value);

    ::int2 Size;

    ::int2 get_Size();
    void set_Size(::int2 value);

    ::float4 SourceRect;

    ::float4 get_SourceRect();
    void set_SourceRect(::float4 value);

    void ComponentAdded(::Entity* entity);

    virtual void Draw();

    void ParentEnabledChange(bool newEnabled);

    RoundedRectComponent();

    ::Entity* get_Parent();

    void set_Parent(::Entity* value);
private:
    uint8_t renderOrder2D;
};
