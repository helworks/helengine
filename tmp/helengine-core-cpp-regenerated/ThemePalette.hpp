#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class ThemeColors;

#include "ThemeColors.hpp"

class ThemePalette
{
public:
    ::ThemeColors* Colors;

    ::ThemeColors* get_Colors();

    ThemePalette(::ThemeColors* colors);
};
