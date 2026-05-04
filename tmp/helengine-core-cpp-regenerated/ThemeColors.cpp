#ifdef DrawText
#undef DrawText
#endif
#include "ThemeColors.hpp"
#include "runtime/array.hpp"
#include "runtime/finally.hpp"
#include "runtime/native_cast.hpp"
#include "runtime/native_datetime.hpp"
#include "runtime/native_dictionary.hpp"
#include "runtime/native_disposable.hpp"
#include "runtime/native_enum.hpp"
#include "runtime/native_event.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_nullable.hpp"
#include "runtime/native_span.hpp"
#include "runtime/native_stack.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_tuple.hpp"
#include "runtime/native_type.hpp"
#include "system/app_context.hpp"
#include "system/bit_converter.hpp"
#include "system/diagnostics/debug.hpp"
#include "system/diagnostics/stopwatch.hpp"
#include "system/guid.hpp"
#include "system/io/file-stream.hpp"
#include "system/io/file.hpp"
#include "system/io/memory-stream.hpp"
#include "system/io/path.hpp"
#include "system/io/stream-reader.hpp"
#include "system/io/stream.hpp"
#include "system/io/string-reader.hpp"
#include "system/math.hpp"
#include "system/number.hpp"
#include "system/security/cryptography/sha256.hpp"
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"
#include "system/text/regular_expressions/regex.hpp"
#include "system/text/string-builder.hpp"

ThemeColors::ThemeColors() : AccentPrimary(), AccentQuaternary(), AccentSecondary(), AccentTertiary(), BackgroundPrimary(), InputForegroundPrimary(), InputForegroundSecondary(), StateDanger(), StateSuccess(), StateWarning(), SurfaceInput(), SurfacePrimary(), TextOnAccent(), TextPrimary(), TextSecondary()
{
}

::byte4 ThemeColors::get_AccentPrimary()
{
return this->AccentPrimary;
}

void ThemeColors::set_AccentPrimary(::byte4 value)
{
this->AccentPrimary = value;
}

::byte4 ThemeColors::get_AccentQuaternary()
{
return this->AccentQuaternary;
}

void ThemeColors::set_AccentQuaternary(::byte4 value)
{
this->AccentQuaternary = value;
}

::byte4 ThemeColors::get_AccentSecondary()
{
return this->AccentSecondary;
}

void ThemeColors::set_AccentSecondary(::byte4 value)
{
this->AccentSecondary = value;
}

::byte4 ThemeColors::get_AccentTertiary()
{
return this->AccentTertiary;
}

void ThemeColors::set_AccentTertiary(::byte4 value)
{
this->AccentTertiary = value;
}

::byte4 ThemeColors::get_BackgroundPrimary()
{
return this->BackgroundPrimary;
}

void ThemeColors::set_BackgroundPrimary(::byte4 value)
{
this->BackgroundPrimary = value;
}

::byte4 ThemeColors::get_InputForegroundPrimary()
{
return this->InputForegroundPrimary;
}

void ThemeColors::set_InputForegroundPrimary(::byte4 value)
{
this->InputForegroundPrimary = value;
}

::byte4 ThemeColors::get_InputForegroundSecondary()
{
return this->InputForegroundSecondary;
}

void ThemeColors::set_InputForegroundSecondary(::byte4 value)
{
this->InputForegroundSecondary = value;
}

::byte4 ThemeColors::get_StateDanger()
{
return this->StateDanger;
}

void ThemeColors::set_StateDanger(::byte4 value)
{
this->StateDanger = value;
}

::byte4 ThemeColors::get_StateSuccess()
{
return this->StateSuccess;
}

void ThemeColors::set_StateSuccess(::byte4 value)
{
this->StateSuccess = value;
}

::byte4 ThemeColors::get_StateWarning()
{
return this->StateWarning;
}

void ThemeColors::set_StateWarning(::byte4 value)
{
this->StateWarning = value;
}

::byte4 ThemeColors::get_SurfaceInput()
{
return this->SurfaceInput;
}

void ThemeColors::set_SurfaceInput(::byte4 value)
{
this->SurfaceInput = value;
}

::byte4 ThemeColors::get_SurfacePrimary()
{
return this->SurfacePrimary;
}

void ThemeColors::set_SurfacePrimary(::byte4 value)
{
this->SurfacePrimary = value;
}

::byte4 ThemeColors::get_TextOnAccent()
{
return this->TextOnAccent;
}

void ThemeColors::set_TextOnAccent(::byte4 value)
{
this->TextOnAccent = value;
}

::byte4 ThemeColors::get_TextPrimary()
{
return this->TextPrimary;
}

void ThemeColors::set_TextPrimary(::byte4 value)
{
this->TextPrimary = value;
}

::byte4 ThemeColors::get_TextSecondary()
{
return this->TextSecondary;
}

void ThemeColors::set_TextSecondary(::byte4 value)
{
this->TextSecondary = value;
}

