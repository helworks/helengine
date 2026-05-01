#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class Entity;

#include "Entity.hpp"

class Component
{
public:
    Component();

    ::Entity* Parent;

    ::Entity* get_Parent();
    void set_Parent(::Entity* value);

    virtual void ComponentAdded(::Entity* entity);

    virtual void ComponentRemoved(::Entity* entity);

    virtual void ParentEnabledChange(bool newEnabled);

    virtual void ParentStaticChange(bool newEnabled);
};
