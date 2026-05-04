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

enum class PointerCursorKind
{
    Default,
    Hand,
    Text,
    ResizeNorthWestSouthEast,
    ResizeNorthEastSouthWest
};
