#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class int2;

#include "runtime/native_string.hpp"
#include "ButtonState.hpp"
#include "int2.hpp"

class MouseState
{
public:
    virtual ~MouseState() = default;

    MouseState();

    int32_t get_HorizontalScrollWheelValue();

    void set_HorizontalScrollWheelValue(int32_t value);

    ::ButtonState get_LeftButton();

    void set_LeftButton(::ButtonState value);

    ::ButtonState get_MiddleButton();

    void set_MiddleButton(::ButtonState value);

    ::int2 get_Position();

    ::ButtonState get_RightButton();

    void set_RightButton(::ButtonState value);

    int32_t get_ScrollWheelValue();

    void set_ScrollWheelValue(int32_t value);

    int32_t get_X();

    void set_X(int32_t value);

    ::ButtonState get_XButton1();

    void set_XButton1(::ButtonState value);

    ::ButtonState get_XButton2();

    void set_XButton2(::ButtonState value);

    int32_t get_Y();

    void set_Y(int32_t value);

    bool Equals(void* obj);

    int32_t GetHashCode();

    MouseState(int32_t x, int32_t y, int32_t scrollWheel, ::ButtonState leftButton, ::ButtonState middleButton, ::ButtonState rightButton, ::ButtonState xButton1, ::ButtonState xButton2);

    MouseState(int32_t x, int32_t y, int32_t scrollWheel, ::ButtonState leftButton, ::ButtonState middleButton, ::ButtonState rightButton, ::ButtonState xButton1, ::ButtonState xButton2, int32_t horizontalScrollWheel);

    std::string ToString();

    friend bool operator!=(::MouseState left, ::MouseState right);

    friend bool operator==(::MouseState left, ::MouseState right);
private:
    static uint8_t LeftButtonFlag;

    static uint8_t MiddleButtonFlag;

    static uint8_t RightButtonFlag;

    static uint8_t XButton1Flag;

    static uint8_t XButton2Flag;

    uint8_t _buttons;

    int32_t _horizontalScrollWheelValue;

    int32_t _scrollWheelValue;

    int32_t _x;

    int32_t _y;
};
