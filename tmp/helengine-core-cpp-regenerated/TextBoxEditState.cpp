#ifdef DrawText
#undef DrawText
#endif
#include "TextBoxEditState.hpp"
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

int32_t TextBoxEditState::get_CursorPosition()
{
return this->CursorPositionValue;}

void TextBoxEditState::set_CursorPosition(int32_t value)
{
this->CursorPositionValue = this->ClampCursor(value);
}

bool TextBoxEditState::get_HasSelection()
{
return this->SelectionAnchorPositionValue != this->CursorPositionValue;}

int32_t TextBoxEditState::get_SelectionEnd()
{
return Math::Max(this->SelectionAnchorPositionValue, this->CursorPositionValue);}

int32_t TextBoxEditState::get_SelectionStart()
{
return Math::Min(this->SelectionAnchorPositionValue, this->CursorPositionValue);}

std::string TextBoxEditState::get_Text()
{
return this->TextValue;}

void TextBoxEditState::set_Text(std::string value)
{
this->TextValue = value;
this->CursorPositionValue = this->ClampCursor(this->CursorPositionValue);
this->SelectionAnchorPositionValue = this->ClampCursor(this->SelectionAnchorPositionValue);
}

void TextBoxEditState::Backspace()
{
    if (this->RemoveSelection())
    {
return;    }
    if (this->CursorPositionValue <= 0)
    {
return;    }
this->TextValue = String::Remove(this->TextValue, this->CursorPositionValue - 1, 1);
this->CursorPositionValue--;
}

void TextBoxEditState::ClearSelection()
{
this->SelectionAnchorPositionValue = this->CursorPositionValue;
}

void TextBoxEditState::Delete()
{
    if (this->RemoveSelection())
    {
return;    }
    if (this->CursorPositionValue < 0 || this->CursorPositionValue >= static_cast<int32_t>(this->TextValue.size()))
    {
return;    }
this->TextValue = String::Remove(this->TextValue, this->CursorPositionValue, 1);
this->CursorPositionValue = this->ClampCursor(this->CursorPositionValue);
}

void TextBoxEditState::InsertCharacter(char character)
{
this->RemoveSelection();
const int32_t insertionIndex = this->ClampCursor(this->CursorPositionValue);
this->TextValue = String::Insert(this->TextValue, insertionIndex, std::string(1, character));
this->CursorPositionValue = insertionIndex + 1;
this->SelectionAnchorPositionValue = this->CursorPositionValue;
}

void TextBoxEditState::MoveCursorLeft()
{
    if (this->get_HasSelection())
    {
this->CursorPositionValue = this->get_SelectionStart();
this->SelectionAnchorPositionValue = this->CursorPositionValue;
return;    }
this->CursorPositionValue = Math::Max(0, this->CursorPositionValue - 1);
}

void TextBoxEditState::MoveCursorRight()
{
    if (this->get_HasSelection())
    {
this->CursorPositionValue = this->get_SelectionEnd();
this->SelectionAnchorPositionValue = this->CursorPositionValue;
return;    }
this->CursorPositionValue = Math::Min(static_cast<int32_t>(this->TextValue.size()), this->CursorPositionValue + 1);
}

TextBoxEditState::TextBoxEditState() : CursorPositionValue(0), SelectionAnchorPositionValue(0), TextValue("")
{
}

TextBoxEditState::TextBoxEditState(std::string text) : CursorPositionValue(0), SelectionAnchorPositionValue(0), TextValue("")
{
this->set_Text(text);
this->SetCursorToEnd();
}

void TextBoxEditState::SelectAll()
{
this->SelectionAnchorPositionValue = 0;
this->CursorPositionValue = static_cast<int32_t>(this->TextValue.size());
}

void TextBoxEditState::SetCursorToEnd()
{
this->CursorPositionValue = static_cast<int32_t>(this->TextValue.size());
this->SelectionAnchorPositionValue = this->CursorPositionValue;
}

void TextBoxEditState::SetCursorToStart()
{
this->CursorPositionValue = 0;
this->SelectionAnchorPositionValue = 0;
}

void TextBoxEditState::SetSelection(int32_t anchorPosition, int32_t cursorPosition)
{
this->SelectionAnchorPositionValue = this->ClampCursor(anchorPosition);
this->CursorPositionValue = this->ClampCursor(cursorPosition);
}

int32_t TextBoxEditState::ClampCursor(int32_t value)
{
return Math::Max(0, Math::Min(value, static_cast<int32_t>(this->TextValue.size())));}

bool TextBoxEditState::RemoveSelection()
{
    if (!this->get_HasSelection())
    {
return false;    }
const int32_t selectionStart = this->get_SelectionStart();
const int32_t selectionLength = this->get_SelectionEnd() - selectionStart;
this->TextValue = String::Remove(this->TextValue, selectionStart, selectionLength);
this->CursorPositionValue = selectionStart;
this->SelectionAnchorPositionValue = selectionStart;
return true;}

