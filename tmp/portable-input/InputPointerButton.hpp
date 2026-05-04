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

enum class InputPointerButton
{
    Primary = 0,
    Secondary = 1,
    Middle = 2,
    Back = 3,
    Forward = 4
};
