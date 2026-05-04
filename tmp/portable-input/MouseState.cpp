#ifdef DrawText
#undef DrawText
#endif
#include "MouseState.hpp"
#include "runtime/native_string.hpp"
#include "ButtonState.hpp"
#include "int2.hpp"
#include "runtime/array.hpp"
#include "runtime/finally.hpp"
#include "runtime/native_dictionary.hpp"
#include "runtime/native_enum.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_string.hpp"
#include "system/math.hpp"

MouseState::MouseState() : _buttons(), _horizontalScrollWheelValue(0), _scrollWheelValue(0), _x(0), _y(0)
{
}

int32_t MouseState::get_HorizontalScrollWheelValue()
{
return this->_horizontalScrollWheelValue;}

void MouseState::set_HorizontalScrollWheelValue(int32_t value)
{
this->_horizontalScrollWheelValue = value;
}

::ButtonState MouseState::get_LeftButton()
{
return ((this->_buttons & LeftButtonFlag) > 0) ? ButtonState::Pressed : ButtonState::Released;}

void MouseState::set_LeftButton(::ButtonState value)
{
    if (value == ButtonState::Pressed)
    {
this->_buttons = static_cast<uint8_t>((this->_buttons | LeftButtonFlag));
    }
else {
this->_buttons = static_cast<uint8_t>((this->_buttons & (~LeftButtonFlag)));
}
}

::ButtonState MouseState::get_MiddleButton()
{
return ((this->_buttons & MiddleButtonFlag) > 0) ? ButtonState::Pressed : ButtonState::Released;}

void MouseState::set_MiddleButton(::ButtonState value)
{
    if (value == ButtonState::Pressed)
    {
this->_buttons = static_cast<uint8_t>((this->_buttons | MiddleButtonFlag));
    }
else {
this->_buttons = static_cast<uint8_t>((this->_buttons & (~MiddleButtonFlag)));
}
}

::int2 MouseState::get_Position()
{
return ::int2(this->_x, this->_y);}

::ButtonState MouseState::get_RightButton()
{
return ((this->_buttons & RightButtonFlag) > 0) ? ButtonState::Pressed : ButtonState::Released;}

void MouseState::set_RightButton(::ButtonState value)
{
    if (value == ButtonState::Pressed)
    {
this->_buttons = static_cast<uint8_t>((this->_buttons | RightButtonFlag));
    }
else {
this->_buttons = static_cast<uint8_t>((this->_buttons & (~RightButtonFlag)));
}
}

int32_t MouseState::get_ScrollWheelValue()
{
return this->_scrollWheelValue;}

void MouseState::set_ScrollWheelValue(int32_t value)
{
this->_scrollWheelValue = value;
}

int32_t MouseState::get_X()
{
return this->_x;}

void MouseState::set_X(int32_t value)
{
this->_x = value;
}

::ButtonState MouseState::get_XButton1()
{
return ((this->_buttons & XButton1Flag) > 0) ? ButtonState::Pressed : ButtonState::Released;}

void MouseState::set_XButton1(::ButtonState value)
{
    if (value == ButtonState::Pressed)
    {
this->_buttons = static_cast<uint8_t>((this->_buttons | XButton1Flag));
    }
else {
this->_buttons = static_cast<uint8_t>((this->_buttons & (~XButton1Flag)));
}
}

::ButtonState MouseState::get_XButton2()
{
return ((this->_buttons & XButton2Flag) > 0) ? ButtonState::Pressed : ButtonState::Released;}

void MouseState::set_XButton2(::ButtonState value)
{
    if (value == ButtonState::Pressed)
    {
this->_buttons = static_cast<uint8_t>((this->_buttons | XButton2Flag));
    }
else {
this->_buttons = static_cast<uint8_t>((this->_buttons & (~XButton2Flag)));
}
}

int32_t MouseState::get_Y()
{
return this->_y;}

void MouseState::set_Y(int32_t value)
{
this->_y = value;
}

bool MouseState::Equals(void* obj)
{
    if (obj != nullptr)
    {
return (*this) == (*static_cast<MouseState*>(obj));    }
return false;}

int32_t MouseState::GetHashCode()
{
{
int32_t hashCode = this->_x;
hashCode = (hashCode * 397) ^ this->_y;
hashCode = (hashCode * 397) ^ this->_scrollWheelValue;
hashCode = (hashCode * 397) ^ this->_horizontalScrollWheelValue;
hashCode = (hashCode * 397) ^ static_cast<int32_t>(this->_buttons);
return hashCode;}
}

MouseState::MouseState(int32_t x, int32_t y, int32_t scrollWheel, ::ButtonState leftButton, ::ButtonState middleButton, ::ButtonState rightButton, ::ButtonState xButton1, ::ButtonState xButton2) : _buttons(), _horizontalScrollWheelValue(0), _scrollWheelValue(0), _x(0), _y(0)
{
this->_x = x;
this->_y = y;
this->_scrollWheelValue = scrollWheel;
this->_buttons = static_cast<uint8_t>(((leftButton == ButtonState::Pressed ? LeftButtonFlag : 0) | (rightButton == ButtonState::Pressed ? RightButtonFlag : 0) | (middleButton == ButtonState::Pressed ? MiddleButtonFlag : 0) | (xButton1 == ButtonState::Pressed ? XButton1Flag : 0) | (xButton2 == ButtonState::Pressed ? XButton2Flag : 0)));
this->_horizontalScrollWheelValue = 0;
}

MouseState::MouseState(int32_t x, int32_t y, int32_t scrollWheel, ::ButtonState leftButton, ::ButtonState middleButton, ::ButtonState rightButton, ::ButtonState xButton1, ::ButtonState xButton2, int32_t horizontalScrollWheel) : _buttons(), _horizontalScrollWheelValue(0), _scrollWheelValue(0), _x(0), _y(0)
{
this->_x = x;
this->_y = y;
this->_scrollWheelValue = scrollWheel;
this->_buttons = static_cast<uint8_t>(((leftButton == ButtonState::Pressed ? LeftButtonFlag : 0) | (rightButton == ButtonState::Pressed ? RightButtonFlag : 0) | (middleButton == ButtonState::Pressed ? MiddleButtonFlag : 0) | (xButton1 == ButtonState::Pressed ? XButton1Flag : 0) | (xButton2 == ButtonState::Pressed ? XButton2Flag : 0)));
this->_horizontalScrollWheelValue = horizontalScrollWheel;
}

std::string MouseState::ToString()
{
std::string buttons;
    if (this->_buttons == 0)
    {
buttons = "None";
    }
else {
buttons = String::Empty;
    if ((this->_buttons & LeftButtonFlag) == LeftButtonFlag)
    {
    if (static_cast<int32_t>(buttons.size()) > 0)
    {
buttons += " Left";
    }
else {
buttons += "Left";
}
    }
    if ((this->_buttons & RightButtonFlag) == RightButtonFlag)
    {
    if (static_cast<int32_t>(buttons.size()) > 0)
    {
buttons += " Right";
    }
else {
buttons += "Right";
}
    }
    if ((this->_buttons & MiddleButtonFlag) == MiddleButtonFlag)
    {
    if (static_cast<int32_t>(buttons.size()) > 0)
    {
buttons += " Middle";
    }
else {
buttons += "Middle";
}
    }
    if ((this->_buttons & XButton1Flag) == XButton1Flag)
    {
    if (static_cast<int32_t>(buttons.size()) > 0)
    {
buttons += " XButton1";
    }
else {
buttons += "XButton1";
}
    }
    if ((this->_buttons & XButton2Flag) == XButton2Flag)
    {
    if (static_cast<int32_t>(buttons.size()) > 0)
    {
buttons += " XButton2";
    }
else {
buttons += "XButton2";
}
    }
}
return String::Concat("[MouseState X=", std::to_string(this->_x), ", Y=", std::to_string(_y), ", Buttons=", buttons, ", Wheel=", std::to_string(_scrollWheelValue), ", HWheel=", std::to_string(_horizontalScrollWheelValue), "]");}

uint8_t MouseState::LeftButtonFlag = 1;

uint8_t MouseState::MiddleButtonFlag = 4;

uint8_t MouseState::RightButtonFlag = 2;

uint8_t MouseState::XButton1Flag = 8;

uint8_t MouseState::XButton2Flag = 16;

bool operator!=(::MouseState left, ::MouseState right)
{
return !(left == right);}

bool operator==(::MouseState left, ::MouseState right)
{
return left._x == right._x && left._y == right._y && left._buttons == right._buttons && left._scrollWheelValue == right._scrollWheelValue && left._horizontalScrollWheelValue == right._horizontalScrollWheelValue;}

