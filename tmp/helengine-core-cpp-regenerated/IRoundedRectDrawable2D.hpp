#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class IDrawable2D;
class byte4;
class int2;
class float4;

#include "IDrawable2D.hpp"
#include "byte4.hpp"
#include "int2.hpp"
#include "float4.hpp"

class IRoundedRectDrawable2D : public IDrawable2D
{
public:
    virtual ::byte4 get_BorderColor() = 0;

    virtual void set_BorderColor(::byte4 value) = 0;

    virtual float get_BorderThickness() = 0;

    virtual void set_BorderThickness(float value) = 0;

    virtual ::byte4 get_Color() = 0;

    virtual void set_Color(::byte4 value) = 0;

    virtual ::byte4 get_FillColor() = 0;

    virtual void set_FillColor(::byte4 value) = 0;

    virtual float get_Radius() = 0;

    virtual void set_Radius(float value) = 0;

    virtual float get_Rotation() = 0;

    virtual void set_Rotation(float value) = 0;

    virtual ::int2 get_Size() = 0;

    virtual void set_Size(::int2 value) = 0;

    virtual ::float4 get_SourceRect() = 0;

    virtual void set_SourceRect(::float4 value) = 0;
};
