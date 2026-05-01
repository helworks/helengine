#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class IRenderQueue2D;
class Logger;
class IDrawable2D;
class IRenderVisitor2D;

#include "IRenderQueue2D.hpp"
#include "runtime/native_list.hpp"
#include "Logger.hpp"
#include "Logger.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_list.hpp"
#include "IDrawable2D.hpp"
#include "IRenderVisitor2D.hpp"

class RenderList2D : public IRenderQueue2D
{
public:
    int32_t get_Count();

    void Add(::IDrawable2D* drawable);

    void Clear();

    void EnsureCapacity(int32_t desiredCount);

    void EnsureCapacity(int32_t desiredCount, bool warnOnExpand);

    bool Remove(::IDrawable2D* drawable);

    RenderList2D(int32_t initialCapacity);

    void VisitOrdered(::IRenderVisitor2D* visitor);
private:
    List<::IDrawable2D*>* items;

    int32_t FindIndexByReference(::IDrawable2D* drawable);

    int32_t FindInsertIndex(uint8_t renderOrder);
};
