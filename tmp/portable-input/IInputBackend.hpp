#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class InputFrameState;

#include "InputFrameState.hpp"

class IInputBackend
{
public:
    virtual ::InputFrameState CaptureFrame() = 0;
};
