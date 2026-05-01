#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class IDrawable2D;

#include "IDrawable2D.hpp"

class IRenderVisitor2D
{
public:
    virtual void Visit(::IDrawable2D* drawable) = 0;
};
