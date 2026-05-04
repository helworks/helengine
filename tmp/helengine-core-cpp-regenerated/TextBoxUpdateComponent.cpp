#ifdef DrawText
#undef DrawText
#endif
#include "TextBoxUpdateComponent.hpp"
#include "TextBoxComponent.hpp"
#include "InputSystem.hpp"
#include "Core.hpp"
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

TextBoxUpdateComponent::TextBoxUpdateComponent(::TextBoxComponent* textBox) : textBox()
{
this->textBox = textBox;
}

void TextBoxUpdateComponent::Update()
{
this->textBox->Update();
    if (!this->textBox->get_IsFocused())
    {
return;    }
InputSystem *input = Core::get_Instance()->get_Input();
    if (!input->WasMouseLeftButtonPressed())
    {
return;    }
const int32_t pointerX = input->GetMouseX();
const int32_t pointerY = input->GetMouseY();
    if (!this->textBox->ContainsScreenPoint(pointerX, pointerY))
    {
this->textBox->set_IsFocused(false);
    }
}

uint8_t TextBoxUpdateComponent::get_UpdateOrder()
{
return this->UpdateComponent::get_UpdateOrder();
}

void TextBoxUpdateComponent::set_UpdateOrder(uint8_t value)
{
this->UpdateComponent::set_UpdateOrder(value);
}

::Entity* TextBoxUpdateComponent::get_Parent()
{
return this->Component::get_Parent();
}

void TextBoxUpdateComponent::set_Parent(::Entity* value)
{
this->Component::set_Parent(value);
}

