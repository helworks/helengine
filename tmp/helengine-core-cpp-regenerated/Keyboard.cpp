#ifdef DrawText
#undef DrawText
#endif
#include "Keyboard.hpp"
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

void Keyboard::SetActive(bool isActive)
{
}

