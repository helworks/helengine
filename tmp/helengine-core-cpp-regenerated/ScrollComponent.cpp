#ifdef DrawText
#undef DrawText
#endif
#include "ScrollComponent.hpp"
#include "float3.hpp"
#include "Entity.hpp"
#include "Core.hpp"
#include "InputSystem.hpp"
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

ScrollComponent::ScrollComponent() : ScrollOffset(0), ScrollOffsetChanged(), ItemCountValue(0), RequiresPointerInsideValue(true), ScrollStepCountValue(1), SizeValue(), VisibleItemCountValue(0), WheelNotchSizeValue(StandardWheelNotch)
{
}

int32_t ScrollComponent::get_ItemCount()
{
return this->ItemCountValue;}

void ScrollComponent::set_ItemCount(int32_t value)
{
    if (value < 0)
    {
throw ([&]() {
auto __ctor_arg_0000019A = "value";
auto __ctor_arg_0000019B = "Item count must be zero or greater.";
return new ArgumentOutOfRangeException(__ctor_arg_0000019A, __ctor_arg_0000019B);
})();
    }
this->ItemCountValue = value;
this->ClampScrollOffset();
}

int32_t ScrollComponent::get_MaximumScrollOffset()
{
return Math::Max(0, this->ItemCountValue - this->VisibleItemCountValue);}

bool ScrollComponent::get_RequiresPointerInside()
{
return this->RequiresPointerInsideValue;}

void ScrollComponent::set_RequiresPointerInside(bool value)
{
this->RequiresPointerInsideValue = value;
}

int32_t ScrollComponent::get_ScrollOffset()
{
return this->ScrollOffset;
}

void ScrollComponent::set_ScrollOffset(int32_t value)
{
this->ScrollOffset = value;
}

int32_t ScrollComponent::get_ScrollStepCount()
{
return this->ScrollStepCountValue;}

void ScrollComponent::set_ScrollStepCount(int32_t value)
{
    if (value < 1)
    {
throw ([&]() {
auto __ctor_arg_0000019C = "value";
auto __ctor_arg_0000019D = "Scroll step count must be at least one.";
return new ArgumentOutOfRangeException(__ctor_arg_0000019C, __ctor_arg_0000019D);
})();
    }
this->ScrollStepCountValue = value;
}

int2* ScrollComponent::get_Size()
{
return this->SizeValue;}

void ScrollComponent::set_Size(int2* value)
{
    if (value->X < 0 || value->Y < 0)
    {
throw ([&]() {
auto __ctor_arg_0000019E = "value";
auto __ctor_arg_0000019F = "Scroll viewport size must not be negative.";
return new ArgumentOutOfRangeException(__ctor_arg_0000019E, __ctor_arg_0000019F);
})();
    }
this->SizeValue = value;
}

int32_t ScrollComponent::get_VisibleItemCount()
{
return this->VisibleItemCountValue;}

void ScrollComponent::set_VisibleItemCount(int32_t value)
{
    if (value < 0)
    {
throw ([&]() {
auto __ctor_arg_000001A0 = "value";
auto __ctor_arg_000001A1 = "Visible item count must be zero or greater.";
return new ArgumentOutOfRangeException(__ctor_arg_000001A0, __ctor_arg_000001A1);
})();
    }
this->VisibleItemCountValue = value;
this->ClampScrollOffset();
}

int32_t ScrollComponent::get_WheelNotchSize()
{
return this->WheelNotchSizeValue;}

void ScrollComponent::set_WheelNotchSize(int32_t value)
{
    if (value < 1)
    {
throw ([&]() {
auto __ctor_arg_000001A2 = "value";
auto __ctor_arg_000001A3 = "Wheel notch size must be at least one.";
return new ArgumentOutOfRangeException(__ctor_arg_000001A2, __ctor_arg_000001A3);
})();
    }
this->WheelNotchSizeValue = value;
}

void ScrollComponent::ClampScrollOffset()
{
this->set_ScrollOffset(this->ClampOffset(this->ScrollOffset));
}

bool ScrollComponent::ContainsScreenPoint(int32_t x, int32_t y)
{
    if (Parent == nullptr)
    {
return false;    }
::float3 origin = Parent->get_Position();
return x >= origin.X && x < origin.X + this->SizeValue->X && y >= origin.Y && y < origin.Y + this->SizeValue->Y;}

void ScrollComponent::ResetScrollOffset()
{
this->set_ScrollOffset(0);
}

bool ScrollComponent::ScrollTo(int32_t scrollOffset)
{
return this->SetScrollOffset(scrollOffset, true);}

bool ScrollComponent::TryApplyWheelInput()
{
    if (Parent == nullptr)
    {
return false;    }
    if (this->get_MaximumScrollOffset() <= 0)
    {
return false;    }
    if (this->RequiresPointerInsideValue && !this->ContainsScreenPoint(Core::get_Instance()->get_Input()->GetMouseX(), Core::get_Instance()->get_Input()->GetMouseY()))
    {
return false;    }
const int32_t wheelDelta = Core::get_Instance()->get_Input()->GetMouseScrollWheelDelta();
    if (wheelDelta == 0)
    {
return false;    }
int32_t scrollSteps = wheelDelta / this->WheelNotchSizeValue;
    if (scrollSteps == 0)
    {
scrollSteps = wheelDelta > 0 ? 1 : -1;
    }
scrollSteps *= this->ScrollStepCountValue;
const int32_t nextOffset = this->ScrollOffset - scrollSteps;
return this->SetScrollOffset(nextOffset, true);}

void ScrollComponent::Update()
{
this->TryApplyWheelInput();
}

uint8_t ScrollComponent::get_UpdateOrder()
{
return this->UpdateComponent::get_UpdateOrder();
}

void ScrollComponent::set_UpdateOrder(uint8_t value)
{
this->UpdateComponent::set_UpdateOrder(value);
}

::Entity* ScrollComponent::get_Parent()
{
return this->Component::get_Parent();
}

void ScrollComponent::set_Parent(::Entity* value)
{
this->Component::set_Parent(value);
}

int32_t ScrollComponent::StandardWheelNotch = 120;

int32_t ScrollComponent::ClampOffset(int32_t scrollOffset)
{
const int32_t maxOffset = this->get_MaximumScrollOffset();
    if (scrollOffset < 0)
    {
return 0;    }
    if (scrollOffset > maxOffset)
    {
return maxOffset;    }
return scrollOffset;}

bool ScrollComponent::SetScrollOffset(int32_t scrollOffset, bool raiseEvent)
{
const int32_t clampedOffset = this->ClampOffset(scrollOffset);
    if (clampedOffset == this->ScrollOffset)
    {
return false;    }
this->set_ScrollOffset(clampedOffset);
    if (raiseEvent && true)
    {
this->ScrollOffsetChanged.Invoke(this, ScrollOffset);
    }
return true;}

