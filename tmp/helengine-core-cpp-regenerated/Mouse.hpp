#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class int2;
class MouseState;

#include "int2.hpp"
#include "MouseState.hpp"
#include "int2.hpp"

class Mouse
{
public:
    virtual ::int2 ConsumePointerWrapDeltaOffset();

    virtual ::MouseState GetState() = 0;

    virtual void SetPointerWrapEnabled(bool isEnabled);

    virtual void SetPosition(int32_t x, int32_t y) = 0;
private:
    static ::MouseState _defaultState;
};
