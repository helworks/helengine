#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

#include "int2.hpp"
#include "runtime/native_event.hpp"

class IAnchorBoundsProvider
{
public:
    virtual int2* get_AnchorBounds() = 0;

    ::Event AnchorBoundsChanged;
};
