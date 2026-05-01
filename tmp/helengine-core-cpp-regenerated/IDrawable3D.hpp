#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class RuntimeMaterial;
class RuntimeModel;
class Entity;

#include "RuntimeMaterial.hpp"
#include "RuntimeModel.hpp"
#include "Entity.hpp"

class IDrawable3D
{
public:
    virtual ::RuntimeMaterial* get_Material() = 0;

    virtual void set_Material(::RuntimeMaterial* value) = 0;

    virtual ::RuntimeModel* get_Model() = 0;

    virtual ::Entity* get_Parent() = 0;

    virtual uint8_t get_RenderOrder3D() = 0;

    virtual void set_RenderOrder3D(uint8_t value) = 0;
};
