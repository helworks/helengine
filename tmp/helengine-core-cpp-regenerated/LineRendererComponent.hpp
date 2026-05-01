#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class Component;

#include "Component.hpp"

class LineRendererComponent : public Component
{
public:
    ::Entity* get_Parent();

    void set_Parent(::Entity* value);
};
