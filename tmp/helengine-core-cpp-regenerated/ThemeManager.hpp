#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class ThemeColors;
class ThemePalette;

#include "ThemeColors.hpp"
#include "ThemeColors.hpp"
#include "ThemePalette.hpp"
#include "ThemePalette.hpp"
#include "runtime/native_event.hpp"

class ThemeManager
{
public:
    virtual ~ThemeManager() = default;

    static ::ThemeColors* get_Colors();

    static ::ThemePalette* Current;

    static ::ThemePalette* get_Current();
    static void set_Current(::ThemePalette* value);

    static ::Event ThemeChanged;

    static ::ThemePalette* CreateDarkTheme();

    static ::ThemePalette* CreateLightTheme();

    static ::ThemePalette* CreateNeon90s();

    static void SetTheme(::ThemePalette* palette);
};
