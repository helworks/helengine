#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class IDrawable3D;

#include "IDrawable3D.hpp"

class IRenderVisitor3D
{
public:
    virtual void Visit(::IDrawable3D* drawable) = 0;
};
