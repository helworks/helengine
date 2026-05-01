#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class KeyboardState;

#include "KeyboardState.hpp"

class Keyboard
{
public:
    virtual ::KeyboardState GetState() = 0;

    virtual void SetActive(bool isActive);
};
