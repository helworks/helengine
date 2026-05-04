#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

#include "InputGamepadButton.hpp"

class InputGamepadState
{
public:
    virtual ~InputGamepadState() = default;

    InputGamepadState();

    uint64_t Buttons;

    uint64_t get_Buttons();
    void set_Buttons(uint64_t value);

    bool Connected;

    bool get_Connected();
    void set_Connected(bool value);

    int16_t LeftStickX;

    int16_t get_LeftStickX();
    void set_LeftStickX(int16_t value);

    int16_t LeftStickY;

    int16_t get_LeftStickY();
    void set_LeftStickY(int16_t value);

    int16_t LeftTrigger;

    int16_t get_LeftTrigger();
    void set_LeftTrigger(int16_t value);

    int16_t RightStickX;

    int16_t get_RightStickX();
    void set_RightStickX(int16_t value);

    int16_t RightStickY;

    int16_t get_RightStickY();
    void set_RightStickY(int16_t value);

    int16_t RightTrigger;

    int16_t get_RightTrigger();
    void set_RightTrigger(int16_t value);

    bool IsButtonDown(::InputGamepadButton button);

    void SetButtonDown(::InputGamepadButton button, bool isDown);
};
