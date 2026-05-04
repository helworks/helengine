#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

#include "InputPointerButton.hpp"

class InputPointerState
{
public:
    virtual ~InputPointerState() = default;

    InputPointerState();

    uint64_t Buttons;

    uint64_t get_Buttons();
    void set_Buttons(uint64_t value);

    bool Connected;

    bool get_Connected();
    void set_Connected(bool value);

    int32_t DeltaX;

    int32_t get_DeltaX();
    void set_DeltaX(int32_t value);

    int32_t DeltaY;

    int32_t get_DeltaY();
    void set_DeltaY(int32_t value);

    int32_t ScrollDelta;

    int32_t get_ScrollDelta();
    void set_ScrollDelta(int32_t value);

    int32_t X;

    int32_t get_X();
    void set_X(int32_t value);

    int32_t Y;

    int32_t get_Y();
    void set_Y(int32_t value);

    bool IsButtonDown(::InputPointerButton button);

    void SetButtonDown(::InputPointerButton button, bool isDown);
};
