#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class Component;
class IDrawable3D;
class Entity;
class Core;
class ObjectManager;
class RuntimeMaterial;
class RuntimeModel;

#include "Component.hpp"
#include "IDrawable3D.hpp"
#include "Component.hpp"
#include "Entity.hpp"
#include "Core.hpp"
#include "Core.hpp"
#include "ObjectManager.hpp"
#include "RuntimeMaterial.hpp"
#include "RuntimeModel.hpp"
#include "Entity.hpp"

class MeshComponent : public Component, public IDrawable3D
{
public:
    virtual ~MeshComponent() = default;

    ::RuntimeMaterial* Material;

    ::RuntimeMaterial* get_Material();
    void set_Material(::RuntimeMaterial* value);

    ::RuntimeModel* Model;

    ::RuntimeModel* get_Model();
    void set_Model(::RuntimeModel* value);

    uint8_t get_RenderOrder3D();

    void set_RenderOrder3D(uint8_t value);

    void ComponentAdded(::Entity* entity);

    MeshComponent();

    void ParentEnabledChange(bool newEnabled);

    ::Entity* get_Parent();

    void set_Parent(::Entity* value);
private:
    uint8_t renderOrder3D;
};
