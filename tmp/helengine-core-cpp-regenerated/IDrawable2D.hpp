#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class Entity;

#include "Entity.hpp"

class IDrawable2D
{
public:
    virtual ::Entity* get_Parent() = 0;

    virtual uint8_t get_RenderOrder2D() = 0;

    virtual void set_RenderOrder2D(uint8_t value) = 0;

    virtual void Draw() = 0;
};
