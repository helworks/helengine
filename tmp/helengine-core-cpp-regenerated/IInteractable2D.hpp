#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class Entity;
class int2;

#include "runtime/native_event.hpp"
#include "PointerCursorKind.hpp"
#include "Entity.hpp"
#include "int2.hpp"
#include "PointerInteraction.hpp"

class IInteractable2D
{
public:
    ::Event CursorEvent;

    virtual ::PointerCursorKind get_HoverCursor() = 0;

    virtual ::Entity* get_Parent() = 0;

    virtual ::int2 get_Size() = 0;

    virtual void set_Size(::int2 value) = 0;

    virtual void OnCursor(::int2 relPos, ::int2 delta, ::PointerInteraction state) = 0;
};
