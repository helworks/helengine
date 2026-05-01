#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class IDrawable3D;
class IRenderVisitor3D;

#include "IDrawable3D.hpp"
#include "IRenderVisitor3D.hpp"

class IRenderQueue3D
{
public:
    virtual int32_t get_Count() = 0;

    virtual void Add(::IDrawable3D* drawable) = 0;

    virtual void Clear() = 0;

    virtual void EnsureCapacity(int32_t desiredCount) = 0;

    virtual bool Remove(::IDrawable3D* drawable) = 0;

    virtual void VisitOrdered(::IRenderVisitor3D* visitor) = 0;
};
