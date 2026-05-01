#ifdef DrawText
#undef DrawText
#endif
#include "Mouse.hpp"
#include "int2.hpp"
#include "MouseState.hpp"
#include "runtime/finally.hpp"
#include "runtime/native_dictionary.hpp"
#include "runtime/native_enum.hpp"
#include "runtime/native_event.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_tuple.hpp"
#include "runtime/native_type.hpp"
#include "system/io/stream.hpp"
#include "system/math.hpp"

::int2 Mouse::ConsumePointerWrapDeltaOffset()
{
return ::int2(_defaultState.get_X(), _defaultState.get_Y());}

void Mouse::SetPointerWrapEnabled(bool isEnabled)
{
}

::MouseState Mouse::_defaultState = ::MouseState();

