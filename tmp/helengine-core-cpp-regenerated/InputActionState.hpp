#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class InputActionState
{
public:
    virtual ~InputActionState() = default;

    InputActionState();

    bool IsDown;

    bool get_IsDown();
    void set_IsDown(bool value);

    float Value;

    float get_Value();
    void set_Value(float value);

    bool WasPressed;

    bool get_WasPressed();
    void set_WasPressed(bool value);

    bool WasReleased;

    bool get_WasReleased();
    void set_WasReleased(bool value);
};
