#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class IRenderQueue3D;
class Logger;
class IDrawable3D;
class IRenderVisitor3D;

#include "IRenderQueue3D.hpp"
#include "runtime/native_list.hpp"
#include "Logger.hpp"
#include "Logger.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_list.hpp"
#include "IDrawable3D.hpp"
#include "IRenderVisitor3D.hpp"

class RenderList3D : public IRenderQueue3D
{
public:
    int32_t get_Count();

    void Add(::IDrawable3D* drawable);

    void Clear();

    void EnsureCapacity(int32_t desiredCount);

    void EnsureCapacity(int32_t desiredCount, bool warnOnExpand);

    bool Remove(::IDrawable3D* drawable);

    RenderList3D(int32_t initialCapacity);

    void VisitOrdered(::IRenderVisitor3D* visitor);
private:
    List<::IDrawable3D*>* items;

    int32_t FindIndexByReference(::IDrawable3D* drawable);

    int32_t FindInsertIndex(uint8_t renderOrder);
};
