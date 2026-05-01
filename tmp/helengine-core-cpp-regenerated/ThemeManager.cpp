#ifdef DrawText
#undef DrawText
#endif
#include "ThemeManager.hpp"
#include "ThemeColors.hpp"
#include "ThemePalette.hpp"
#include "byte4.hpp"
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
#include "system/io/directory.hpp"
#include "system/io/file-stream.hpp"
#include "system/io/file.hpp"
#include "system/io/memory-stream.hpp"
#include "system/io/path.hpp"
#include "system/io/stream.hpp"
#include "system/io/string-reader.hpp"
#include "system/math.hpp"
#include "system/number.hpp"
#include "system/security/cryptography/sha256.hpp"
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"
#include "system/text/regular_expressions/regex.hpp"
#include "system/text/string-builder.hpp"

::ThemeColors* ThemeManager::get_Colors()
{
return Current->get_Colors();
}

::ThemePalette* ThemeManager::Current = CreateNeon90s();

::ThemePalette* ThemeManager::get_Current()
{
return ThemeManager::Current;
}

void ThemeManager::set_Current(::ThemePalette* value)
{
ThemeManager::Current = value;
}

::Event ThemeManager::ThemeChanged;

::ThemePalette* ThemeManager::CreateDarkTheme()
{
::ThemeColors *colors = ([&]() {
auto __object_997a35d9 = new ::ThemeColors();
__object_997a35d9->set_BackgroundPrimary(::byte4(20, 20, 20, 255));
__object_997a35d9->set_SurfacePrimary(::byte4(35, 35, 35, 255));
__object_997a35d9->set_SurfaceInput(::byte4(10, 10, 10, 255));
__object_997a35d9->set_AccentPrimary(::byte4(0, 123, 255, 255));
__object_997a35d9->set_AccentSecondary(::byte4(108, 117, 125, 255));
__object_997a35d9->set_AccentTertiary(::byte4(52, 58, 64, 255));
__object_997a35d9->set_AccentQuaternary(::byte4(173, 181, 189, 255));
__object_997a35d9->set_StateWarning(::byte4(255, 193, 7, 255));
__object_997a35d9->set_StateDanger(::byte4(220, 53, 69, 255));
__object_997a35d9->set_StateSuccess(::byte4(40, 167, 69, 255));
__object_997a35d9->set_InputForegroundPrimary(::byte4(0, 255, 0, 255));
__object_997a35d9->set_InputForegroundSecondary(::byte4(255, 255, 0, 255));
__object_997a35d9->set_TextPrimary(::byte4(255, 255, 255, 255));
__object_997a35d9->set_TextSecondary(::byte4(173, 181, 189, 255));
__object_997a35d9->set_TextOnAccent(::byte4(255, 255, 255, 255));
return __object_997a35d9;
})();
return new ::ThemePalette(colors);}

::ThemePalette* ThemeManager::CreateLightTheme()
{
::ThemeColors *colors = ([&]() {
auto __object_c386dd42 = new ::ThemeColors();
__object_c386dd42->set_BackgroundPrimary(::byte4(248, 249, 250, 255));
__object_c386dd42->set_SurfacePrimary(::byte4(255, 255, 255, 255));
__object_c386dd42->set_SurfaceInput(::byte4(233, 236, 239, 255));
__object_c386dd42->set_AccentPrimary(::byte4(0, 123, 255, 255));
__object_c386dd42->set_AccentSecondary(::byte4(108, 117, 125, 255));
__object_c386dd42->set_AccentTertiary(::byte4(52, 58, 64, 255));
__object_c386dd42->set_AccentQuaternary(::byte4(73, 80, 87, 255));
__object_c386dd42->set_StateWarning(::byte4(255, 193, 7, 255));
__object_c386dd42->set_StateDanger(::byte4(220, 53, 69, 255));
__object_c386dd42->set_StateSuccess(::byte4(40, 167, 69, 255));
__object_c386dd42->set_InputForegroundPrimary(::byte4(33, 37, 41, 255));
__object_c386dd42->set_InputForegroundSecondary(::byte4(52, 58, 64, 255));
__object_c386dd42->set_TextPrimary(::byte4(33, 37, 41, 255));
__object_c386dd42->set_TextSecondary(::byte4(73, 80, 87, 255));
__object_c386dd42->set_TextOnAccent(::byte4(255, 255, 255, 255));
return __object_c386dd42;
})();
return new ::ThemePalette(colors);}

::ThemePalette* ThemeManager::CreateNeon90s()
{
::ThemeColors *colors = ([&]() {
auto __object_33ff7bd3 = new ::ThemeColors();
__object_33ff7bd3->set_BackgroundPrimary(::byte4(25, 15, 35, 255));
__object_33ff7bd3->set_SurfacePrimary(::byte4(40, 25, 50, 255));
__object_33ff7bd3->set_SurfaceInput(::byte4(15, 15, 15, 255));
__object_33ff7bd3->set_AccentPrimary(::byte4(194, 49, 175, 255));
__object_33ff7bd3->set_AccentSecondary(::byte4(141, 49, 194, 255));
__object_33ff7bd3->set_AccentTertiary(::byte4(68, 49, 194, 255));
__object_33ff7bd3->set_AccentQuaternary(::byte4(204, 204, 204, 255));
__object_33ff7bd3->set_StateWarning(::byte4(255, 178, 102, 255));
__object_33ff7bd3->set_StateDanger(::byte4(255, 80, 80, 255));
__object_33ff7bd3->set_StateSuccess(::byte4(102, 255, 153, 255));
__object_33ff7bd3->set_InputForegroundPrimary(::byte4(0, 255, 0, 255));
__object_33ff7bd3->set_InputForegroundSecondary(::byte4(255, 255, 0, 255));
__object_33ff7bd3->set_TextPrimary(::byte4(25, 15, 35, 255));
__object_33ff7bd3->set_TextSecondary(::byte4(40, 25, 50, 255));
__object_33ff7bd3->set_TextOnAccent(::byte4(25, 15, 35, 255));
return __object_33ff7bd3;
})();
return new ::ThemePalette(colors);}

void ThemeManager::SetTheme(::ThemePalette* palette)
{
ThemeManager::set_Current((palette != nullptr ? palette : throw new ArgumentNullException("palette")));
ThemeChanged.Invoke(nullptr, nullptr);
}

