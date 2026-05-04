#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class byte4;

#include "byte4.hpp"

class ThemeColors
{
public:
    virtual ~ThemeColors() = default;

    ThemeColors();

    ::byte4 AccentPrimary;

    ::byte4 get_AccentPrimary();
    void set_AccentPrimary(::byte4 value);

    ::byte4 AccentQuaternary;

    ::byte4 get_AccentQuaternary();
    void set_AccentQuaternary(::byte4 value);

    ::byte4 AccentSecondary;

    ::byte4 get_AccentSecondary();
    void set_AccentSecondary(::byte4 value);

    ::byte4 AccentTertiary;

    ::byte4 get_AccentTertiary();
    void set_AccentTertiary(::byte4 value);

    ::byte4 BackgroundPrimary;

    ::byte4 get_BackgroundPrimary();
    void set_BackgroundPrimary(::byte4 value);

    ::byte4 InputForegroundPrimary;

    ::byte4 get_InputForegroundPrimary();
    void set_InputForegroundPrimary(::byte4 value);

    ::byte4 InputForegroundSecondary;

    ::byte4 get_InputForegroundSecondary();
    void set_InputForegroundSecondary(::byte4 value);

    ::byte4 StateDanger;

    ::byte4 get_StateDanger();
    void set_StateDanger(::byte4 value);

    ::byte4 StateSuccess;

    ::byte4 get_StateSuccess();
    void set_StateSuccess(::byte4 value);

    ::byte4 StateWarning;

    ::byte4 get_StateWarning();
    void set_StateWarning(::byte4 value);

    ::byte4 SurfaceInput;

    ::byte4 get_SurfaceInput();
    void set_SurfaceInput(::byte4 value);

    ::byte4 SurfacePrimary;

    ::byte4 get_SurfacePrimary();
    void set_SurfacePrimary(::byte4 value);

    ::byte4 TextOnAccent;

    ::byte4 get_TextOnAccent();
    void set_TextOnAccent(::byte4 value);

    ::byte4 TextPrimary;

    ::byte4 get_TextPrimary();
    void set_TextPrimary(::byte4 value);

    ::byte4 TextSecondary;

    ::byte4 get_TextSecondary();
    void set_TextSecondary(::byte4 value);
};
