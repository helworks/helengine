#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

#include "runtime/native_enum.hpp"
#include "IComparable.hpp"
#include "IConvertible.hpp"
#include "ISpanFormattable.hpp"
#include "IFormattable.hpp"

enum class InputGamepadButton
{
    South = 0,
    East = 1,
    West = 2,
    North = 3,
    LeftShoulder = 4,
    RightShoulder = 5,
    LeftStick = 6,
    RightStick = 7,
    LeftTrigger = 8,
    RightTrigger = 9,
    DPadUp = 10,
    DPadDown = 11,
    DPadLeft = 12,
    DPadRight = 13,
    Start = 14,
    Select = 15,
    Home = 16
};
