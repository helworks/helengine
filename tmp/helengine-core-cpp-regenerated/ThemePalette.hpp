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
    virtual ~ThemePalette() = default;

    ::ThemeColors* Colors;

    ::ThemeColors* get_Colors();

    ThemePalette(::ThemeColors* colors);
};
