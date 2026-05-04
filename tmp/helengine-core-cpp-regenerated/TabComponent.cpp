#ifdef DrawText
#undef DrawText
#endif
#include "TabComponent.hpp"
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
#include "system/binary_primitives.hpp"
#include "system/bit_converter.hpp"
#include "system/diagnostics/debug.hpp"
#include "system/diagnostics/stopwatch.hpp"
#include "system/guid.hpp"
#include "system/io/directory.hpp"
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

bool TabComponent::get_IsSelected()
{
return IsKeyboardFocused;
}

void TabComponent::SetSelected(bool isSelected)
{
SetTargetFocused(isSelected);
}

TabComponent::TabComponent(std::string text, int2* size, ::FontAsset* font, Action<>* onClickAction, float borderThickness) : ButtonComponent(text, size, font, onClickAction, borderThickness)
{
UseTopCorners();
}

int2* TabComponent::get_AnchorSize()
{
return this->ButtonComponent::get_AnchorSize();
}

bool TabComponent::get_CanReceiveFocus()
{
return this->ButtonComponent::get_CanReceiveFocus();
}

::RoundedRectCorners TabComponent::get_Corners()
{
return this->ButtonComponent::get_Corners();
}

void TabComponent::set_Corners(::RoundedRectCorners value)
{
this->ButtonComponent::set_Corners(value);
}

::IFocusGroup* TabComponent::get_FocusGroup()
{
return this->ButtonComponent::get_FocusGroup();
}

void TabComponent::set_FocusGroup(::IFocusGroup* value)
{
this->ButtonComponent::set_FocusGroup(value);
}

bool TabComponent::get_IsDefaultTarget()
{
return this->ButtonComponent::get_IsDefaultTarget();
}

void TabComponent::set_IsDefaultTarget(bool value)
{
this->ButtonComponent::set_IsDefaultTarget(value);
}

bool TabComponent::get_IsKeyboardFocused()
{
return this->ButtonComponent::get_IsKeyboardFocused();
}

void TabComponent::set_IsKeyboardFocused(bool value)
{
this->ButtonComponent::set_IsKeyboardFocused(value);
}

int2* TabComponent::get_Size()
{
return this->ButtonComponent::get_Size();
}

int32_t TabComponent::get_TabIndex()
{
return this->ButtonComponent::get_TabIndex();
}

void TabComponent::set_TabIndex(int32_t value)
{
this->ButtonComponent::set_TabIndex(value);
}

::Entity* TabComponent::get_Parent()
{
return this->Component::get_Parent();
}

void TabComponent::set_Parent(::Entity* value)
{
this->Component::set_Parent(value);
}

