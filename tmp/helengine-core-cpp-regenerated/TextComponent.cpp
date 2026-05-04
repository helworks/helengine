#ifdef DrawText
#undef DrawText
#endif
#include "TextComponent.hpp"
#include "Component.hpp"
#include "Entity.hpp"
#include "Core.hpp"
#include "ObjectManager.hpp"
#include "runtime/native_string.hpp"
#include "system/math.hpp"
#include "FontChar.hpp"
#include "float3.hpp"
#include "float2.hpp"
#include "FontAsset.hpp"
#include "InputSystem.hpp"
#include "RenderManager2D.hpp"
#include "byte4.hpp"
#include "float4.hpp"
#include "RoundedRectComponent.hpp"
#include "ThemeManager.hpp"
#include "TextComponentSelectionUpdateComponent.hpp"
#include "runtime/array.hpp"
#include "runtime/finally.hpp"
#include "runtime/native_cast.hpp"
#include "runtime/native_datetime.hpp"
#include "runtime/native_dictionary.hpp"
#include "runtime/native_disposable.hpp"
#include "runtime/native_enum.hpp"
#include "runtime/native_equatable.hpp"
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

::byte4 TextComponent::get_Color()
{
return this->Color;
}

void TextComponent::set_Color(::byte4 value)
{
this->Color = value;
}

::FontAsset* TextComponent::get_Font()
{
return this->Font;
}

void TextComponent::set_Font(::FontAsset* value)
{
this->Font = value;
}

bool TextComponent::get_HasSelection()
{
return this->SelectionAnchorPositionValue != this->CursorPositionValue;}

uint8_t TextComponent::get_LayerMask()
{
return this->LayerMask;
}

void TextComponent::set_LayerMask(uint8_t value)
{
this->LayerMask = value;
}

uint8_t TextComponent::get_RenderOrder2D()
{
return this->RenderOrder2DValue;}

void TextComponent::set_RenderOrder2D(uint8_t value)
{
    if (this->RenderOrder2DValue != value)
    {
this->RenderOrder2DValue = value;
this->UpdateSelectionRenderOrder();
    if (Parent != nullptr && Parent->get_IsHierarchyEnabled())
    {
Core::get_Instance()->get_ObjectManager()->RemoveFromRender2D(this);
Core::get_Instance()->get_ObjectManager()->RegisterForRender2D(this);
    }
    }
}

float TextComponent::get_Rotation()
{
return this->Rotation;
}

void TextComponent::set_Rotation(float value)
{
this->Rotation = value;
}

bool TextComponent::get_SelectionEnabled()
{
return this->SelectionEnabledValue;}

void TextComponent::set_SelectionEnabled(bool value)
{
    if (this->SelectionEnabledValue == value)
    {
return;    }
this->SelectionEnabledValue = value;
    if (!this->SelectionEnabledValue)
    {
this->IsFocusedValue = false;
this->IsSelectingTextValue = false;
this->ClearSelection();
    if (this->SelectionEntityValue != nullptr)
    {
this->SelectionEntityValue->set_Enabled(false);
    }
this->UpdateSelectionVisual();
return;    }
    if (Parent != nullptr)
    {
this->EnsureSelectionInfrastructure(Parent);
    if (this->SelectionEntityValue != nullptr)
    {
this->SelectionEntityValue->set_Enabled(Parent->get_IsHierarchyEnabled());
    }
    if (Parent->get_IsHierarchyEnabled() && this->RenderOrder2DValue == 0)
    {
Core::get_Instance()->get_ObjectManager()->RemoveFromRender2D(this);
Core::get_Instance()->get_ObjectManager()->RegisterForRender2D(this);
    }
this->UpdateSelectionVisual();
    }
}

int32_t TextComponent::get_SelectionEnd()
{
return Math::Max(this->SelectionAnchorPositionValue, this->CursorPositionValue);}

int32_t TextComponent::get_SelectionStart()
{
return Math::Min(this->SelectionAnchorPositionValue, this->CursorPositionValue);}

int2* TextComponent::get_Size()
{
return this->Size;
}

void TextComponent::set_Size(int2* value)
{
this->Size = value;
}

::float4 TextComponent::get_SourceRect()
{
return this->SourceRect;
}

void TextComponent::set_SourceRect(::float4 value)
{
this->SourceRect = value;
}

std::string TextComponent::get_Text()
{
return this->TextValue;}

void TextComponent::set_Text(std::string value)
{
this->TextValue = value;
this->ClampSelectionToTextLength();
this->UpdateSelectionVisual();
}

::RuntimeTexture* TextComponent::get_Texture()
{
return this->Texture;
}

void TextComponent::set_Texture(::RuntimeTexture* value)
{
this->Texture = value;
}

bool TextComponent::get_WrapText()
{
return this->WrapText;
}

void TextComponent::set_WrapText(bool value)
{
this->WrapText = value;
}

void TextComponent::ClearSelection()
{
this->SelectionAnchorPositionValue = this->CursorPositionValue;
this->UpdateSelectionVisual();
}

void TextComponent::ComponentAdded(::Entity* entity)
{
    if (this->get_SelectionEnabled())
    {
this->EnsureSelectionInfrastructure(entity);
    }
Component::ComponentAdded(entity);
    if (entity->get_IsHierarchyEnabled())
    {
Core::get_Instance()->get_ObjectManager()->RegisterForRender2D(this);
    }
    if (this->get_SelectionEnabled())
    {
    if (this->SelectionEntityValue != nullptr)
    {
this->SelectionEntityValue->set_Enabled(entity->get_IsHierarchyEnabled());
    }
this->UpdateSelectionVisual();
    }
}

void TextComponent::ComponentRemoved(::Entity* entity)
{
Component::ComponentRemoved(entity);
this->IsFocusedValue = false;
this->IsSelectingTextValue = false;
    if (this->SelectionUpdateComponentValue != nullptr && entity != nullptr)
    {
entity->RemoveComponent(this->SelectionUpdateComponentValue);
this->SelectionUpdateComponentValue = nullptr;
    }
    if (this->SelectionEntityValue != nullptr)
    {
this->SelectionEntityValue->set_Enabled(false);
    }
}

void TextComponent::Draw()
{
    if (this->Font == nullptr)
    {
return;    }
Core::get_Instance()->get_RenderManager2D()->DrawText(this);
}

void TextComponent::ParentEnabledChange(bool newEnabled)
{
Component::ParentEnabledChange(newEnabled);
    if (newEnabled)
    {
Core::get_Instance()->get_ObjectManager()->RegisterForRender2D(this);
    }
else {
Core::get_Instance()->get_ObjectManager()->RemoveFromRender2D(this);
this->IsFocusedValue = false;
this->IsSelectingTextValue = false;
}
    if (this->SelectionEntityValue != nullptr)
    {
this->SelectionEntityValue->set_Enabled(newEnabled && this->get_SelectionEnabled());
    }
this->UpdateSelectionVisual();
}

void TextComponent::SelectAll()
{
this->SelectionAnchorPositionValue = 0;
this->CursorPositionValue = static_cast<int32_t>(this->TextValue.size());
this->UpdateSelectionVisual();
}

TextComponent::TextComponent() : Color(), Font(), LayerMask(), Rotation(), Size(), SourceRect(), Texture(), WrapText(), CursorPositionValue(0), IsFocusedValue(), IsSelectingTextValue(), RenderOrder2DValue(), SelectionAnchorPositionValue(0), SelectionEnabledValue(), SelectionEntityValue(), SelectionSpriteValue(), SelectionUpdateComponentValue(), TextValue()
{
this->TextValue = "";
this->SelectionAnchorPositionValue = 0;
this->CursorPositionValue = 0;
this->set_Color(::byte4(255, 255, 255, 255));
this->set_SourceRect(::float4(0, 0, 1, 1));
this->set_WrapText(false);
}

void TextComponent::UpdateSelectionInput()
{
    if (!this->get_SelectionEnabled() || Parent == nullptr || this->Font == nullptr || !Parent->get_IsHierarchyEnabled())
    {
return;    }
InputSystem *input = Core::get_Instance()->get_Input();
    if (input == nullptr)
    {
return;    }
const int32_t pointerX = input->GetMouseX();
const int32_t pointerY = input->GetMouseY();
    if (input->WasMouseLeftButtonPressed())
    {
this->HandleSelectionPress(pointerX, pointerY);
    }
else     if (input->GetMouseLeftButtonState() == ButtonState::Pressed && this->IsSelectingTextValue)
    {
this->HandleSelectionDrag(pointerX, pointerY);
    }
else     if (input->WasMouseLeftButtonReleased())
    {
this->HandleSelectionRelease();
    }
else {
this->HandleSelectionKeyboardInput();
}
}

::Entity* TextComponent::get_Parent()
{
return this->Component::get_Parent();
}

void TextComponent::set_Parent(::Entity* value)
{
this->Component::set_Parent(value);
}

void TextComponent::ClampSelectionToTextLength()
{
const int32_t textLength = static_cast<int32_t>(this->TextValue.size());
    if (this->CursorPositionValue < 0)
    {
this->CursorPositionValue = 0;
    }
else     if (this->CursorPositionValue > textLength)
    {
this->CursorPositionValue = textLength;
    }
    if (this->SelectionAnchorPositionValue < 0)
    {
this->SelectionAnchorPositionValue = 0;
    }
else     if (this->SelectionAnchorPositionValue > textLength)
    {
this->SelectionAnchorPositionValue = textLength;
    }
}

bool TextComponent::ContainsScreenPoint(int32_t x, int32_t y)
{
    if (Parent == nullptr || this->Font == nullptr)
    {
return false;    }
::float3 position = Parent->get_Position();
::float2 textSize = this->Font->MeasureString(this->TextValue);
const double textWidth = textSize.X;
const double textHeight = textSize.Y;
return x >= position.X && x < position.X + textWidth && y >= position.Y && y < position.Y + textHeight;}

void TextComponent::EnsureSelectionInfrastructure(::Entity* entity)
{
    if (this->SelectionEntityValue == nullptr)
    {
    if (entity->get_Children() == nullptr)
    {
entity->InitChildren();
    }
this->SelectionEntityValue = new ::Entity();
this->SelectionEntityValue->set_LayerMask(entity->get_LayerMask());
this->SelectionEntityValue->set_Enabled(false);
this->SelectionEntityValue->InitComponents();
entity->AddChild(this->SelectionEntityValue);
this->SelectionSpriteValue = new ::RoundedRectComponent();
this->SelectionSpriteValue->set_Radius(2.0f);
this->SelectionSpriteValue->set_BorderThickness(0.0f);
this->SelectionSpriteValue->set_FillColor(::byte4(ThemeManager::get_Colors()->get_AccentPrimary().X, ThemeManager::get_Colors()->get_AccentPrimary().Y, ThemeManager::get_Colors()->get_AccentPrimary().Z, 96));
this->SelectionSpriteValue->set_BorderColor(this->SelectionSpriteValue->get_FillColor());
this->SelectionSpriteValue->set_RenderOrder2D(this->ResolveSelectionRenderOrder());
this->SelectionEntityValue->AddComponent(this->SelectionSpriteValue);
    }
    if (this->SelectionUpdateComponentValue == nullptr)
    {
this->SelectionUpdateComponentValue = new ::TextComponentSelectionUpdateComponent(this);
this->SelectionUpdateComponentValue->set_UpdateOrder(Core::get_Instance()->get_ObjectManager()->GetUpdateOrderForLayer(1));
entity->AddComponent(this->SelectionUpdateComponentValue);
    }
}

void TextComponent::HandleSelectionDrag(int32_t pointerX, int32_t pointerY)
{
    if (!this->get_SelectionEnabled() || !this->IsSelectingTextValue || this->Font == nullptr)
    {
return;    }
this->CursorPositionValue = this->ResolveCursorPositionFromClick(pointerX, pointerY);
this->ClampSelectionToTextLength();
this->UpdateSelectionVisual();
}

void TextComponent::HandleSelectionKeyboardInput()
{
    if (!this->get_SelectionEnabled() || !this->IsFocusedValue)
    {
return;    }
InputSystem *input = Core::get_Instance()->get_Input();
const bool isShiftPressed = input->IsKeyDown(Keys::LeftShift) || input->IsKeyDown(Keys::RightShift);
const bool isControlPressed = input->IsKeyDown(Keys::LeftControl) || input->IsKeyDown(Keys::RightControl);
for (int32_t i = 0; i < 255; i++) {
Keys key = static_cast<Keys>(i);
    if (!input->WasKeyPressed(key))
    {
continue;
    }
    if (isControlPressed && key == Keys::A)
    {
this->SelectAll();
    }
else     if (key == Keys::Left)
    {
this->MoveCursorLeft(isShiftPressed);
    }
else     if (key == Keys::Right)
    {
this->MoveCursorRight(isShiftPressed);
    }
else     if (key == Keys::Home)
    {
this->MoveCursorToStart(isShiftPressed);
    }
else     if (key == Keys::End)
    {
this->MoveCursorToEnd(isShiftPressed);
    }
}
}

void TextComponent::HandleSelectionPress(int32_t pointerX, int32_t pointerY)
{
    if (!this->get_SelectionEnabled() || !this->ContainsScreenPoint(pointerX, pointerY))
    {
this->SetFocusedState(false);
return;    }
this->SetFocusedState(true);
const int32_t cursor = this->ResolveCursorPositionFromClick(pointerX, pointerY);
this->CursorPositionValue = cursor;
this->SelectionAnchorPositionValue = cursor;
this->IsSelectingTextValue = true;
this->UpdateSelectionVisual();
}

void TextComponent::HandleSelectionRelease()
{
this->IsSelectingTextValue = false;
}

void TextComponent::MoveCursorLeft(bool extendSelection)
{
    if (extendSelection)
    {
    if (!this->get_HasSelection())
    {
this->SelectionAnchorPositionValue = this->CursorPositionValue;
    }
this->CursorPositionValue = Math::Max(0, this->CursorPositionValue - 1);
    }
else     if (this->get_HasSelection())
    {
this->CursorPositionValue = this->get_SelectionStart();
this->SelectionAnchorPositionValue = this->CursorPositionValue;
    }
else {
this->CursorPositionValue = Math::Max(0, this->CursorPositionValue - 1);
this->SelectionAnchorPositionValue = this->CursorPositionValue;
}
this->UpdateSelectionVisual();
}

void TextComponent::MoveCursorRight(bool extendSelection)
{
    if (extendSelection)
    {
    if (!this->get_HasSelection())
    {
this->SelectionAnchorPositionValue = this->CursorPositionValue;
    }
this->CursorPositionValue = Math::Min(static_cast<int32_t>(this->TextValue.size()), this->CursorPositionValue + 1);
    }
else     if (this->get_HasSelection())
    {
this->CursorPositionValue = this->get_SelectionEnd();
this->SelectionAnchorPositionValue = this->CursorPositionValue;
    }
else {
this->CursorPositionValue = Math::Min(static_cast<int32_t>(this->TextValue.size()), this->CursorPositionValue + 1);
this->SelectionAnchorPositionValue = this->CursorPositionValue;
}
this->UpdateSelectionVisual();
}

void TextComponent::MoveCursorToEnd(bool extendSelection)
{
    if (extendSelection)
    {
    if (!this->get_HasSelection())
    {
this->SelectionAnchorPositionValue = this->CursorPositionValue;
    }
this->CursorPositionValue = static_cast<int32_t>(this->TextValue.size());
    }
else {
this->CursorPositionValue = static_cast<int32_t>(this->TextValue.size());
this->SelectionAnchorPositionValue = this->CursorPositionValue;
}
this->UpdateSelectionVisual();
}

void TextComponent::MoveCursorToStart(bool extendSelection)
{
    if (extendSelection)
    {
    if (!this->get_HasSelection())
    {
this->SelectionAnchorPositionValue = this->CursorPositionValue;
    }
this->CursorPositionValue = 0;
    }
else {
this->CursorPositionValue = 0;
this->SelectionAnchorPositionValue = this->CursorPositionValue;
}
this->UpdateSelectionVisual();
}

double TextComponent::ResolveCharacterAdvance(char character)
{
    if (character == '\r' || character == '\n')
    {
return 0.0;    }
    if (character == ' ')
    {
return Math::Max(static_cast<double>(this->Font->get_FontInfo()->get_SpaceWidth()), 1.0);    }
::FontChar glyph;
    if (this->Font->get_Characters() != nullptr && this->Font->get_Characters()->TryGetValue(character, glyph))
    {
    if (glyph.AdvanceWidth > 0.0f)
    {
return glyph.AdvanceWidth;    }
const double sourceWidth = static_cast<double>(glyph.SourceRect.Z);
    if (sourceWidth > 0.0)
    {
return sourceWidth;    }
    }
return 1.0;}

int32_t TextComponent::ResolveCursorPositionFromClick(int32_t pointerX, int32_t pointerY)
{
    if (this->Font == nullptr || Parent == nullptr)
    {
return 0;    }
    if (String::IsNullOrEmpty(this->TextValue))
    {
return 0;    }
::float3 textPosition = Parent->get_Position();
const double textX = Math::Max(0.0, static_cast<double>(pointerX) - textPosition.X);
const double textY = Math::Max(0.0, static_cast<double>(pointerY) - textPosition.Y);
const int32_t lineIndex = this->ResolveLineIndexFromLocalY(textY);
const int32_t lineStartIndex = this->ResolveLineStartIndex(lineIndex);
const int32_t lineEndIndex = this->ResolveLineEndIndex(lineIndex);
double cursorX = 0.0;
for (int32_t index = lineStartIndex; index < lineEndIndex; index++) {
const double advance = this->ResolveCharacterAdvance(this->TextValue[index]);
    if (textX < cursorX + (advance * 0.5))
    {
return index;    }
cursorX += advance;
}
return lineEndIndex;}

int32_t TextComponent::ResolveLineCount()
{
    if (String::IsNullOrEmpty(this->TextValue))
    {
return 1;    }
int32_t lineCount = 1;
for (int32_t index = 0; index < static_cast<int32_t>(this->TextValue.size()); index++) {
    if (this->TextValue[index] == '\n')
    {
lineCount++;
    }
}
return lineCount;}

int32_t TextComponent::ResolveLineEndIndex(int32_t lineIndex)
{
const int32_t lineStartIndex = this->ResolveLineStartIndex(lineIndex);
for (int32_t index = lineStartIndex; index < static_cast<int32_t>(this->TextValue.size()); index++) {
    if (this->TextValue[index] == '\n')
    {
return index;    }
}
return static_cast<int32_t>(this->TextValue.size());}

int32_t TextComponent::ResolveLineIndexFromLocalY(double localY)
{
const double lineHeight = Math::Max(static_cast<double>(this->Font->get_LineHeight()), 1.0);
const int32_t lineIndex = static_cast<int32_t>((localY / lineHeight));
const int32_t maxLineIndex = Math::Max(0, this->ResolveLineCount() - 1);
return Math::Max(0, Math::Min(lineIndex, maxLineIndex));}

int32_t TextComponent::ResolveLineIndexFromTextIndex(int32_t textIndex)
{
const int32_t clampedIndex = Math::Max(0, Math::Min(textIndex, static_cast<int32_t>(this->TextValue.size())));
int32_t lineIndex = 0;
for (int32_t index = 0; index < clampedIndex && index < static_cast<int32_t>(this->TextValue.size()); index++) {
    if (this->TextValue[index] == '\n')
    {
lineIndex++;
    }
}
return lineIndex;}

int32_t TextComponent::ResolveLineStartIndex(int32_t lineIndex)
{
    if (lineIndex <= 0)
    {
return 0;    }
int32_t currentLineIndex = 0;
for (int32_t index = 0; index < static_cast<int32_t>(this->TextValue.size()); index++) {
    if (this->TextValue[index] != '\n')
    {
continue;
    }
currentLineIndex++;
    if (currentLineIndex == lineIndex)
    {
return index + 1;    }
}
return static_cast<int32_t>(this->TextValue.size());}

uint8_t TextComponent::ResolveSelectionRenderOrder()
{
    if (this->RenderOrder2DValue == 0)
    {
return 0;    }
return static_cast<uint8_t>((this->RenderOrder2DValue - 1));}

double TextComponent::ResolveTextWidth(int32_t startIndex, int32_t endIndex)
{
    if (this->Font == nullptr)
    {
return 0.0;    }
const int32_t clampedStart = Math::Max(0, Math::Min(startIndex, static_cast<int32_t>(this->TextValue.size())));
const int32_t clampedEnd = Math::Max(0, Math::Min(endIndex, static_cast<int32_t>(this->TextValue.size())));
double width = 0.0;
for (int32_t index = clampedStart; index < clampedEnd; index++) {
width += this->ResolveCharacterAdvance(this->TextValue[index]);
}
return width;}

double TextComponent::ResolveTextWidthInLine(int32_t lineIndex, int32_t startIndex, int32_t endIndex)
{
const int32_t lineStartIndex = this->ResolveLineStartIndex(lineIndex);
const int32_t lineEndIndex = this->ResolveLineEndIndex(lineIndex);
const int32_t clampedStartIndex = Math::Max(lineStartIndex, Math::Min(startIndex, lineEndIndex));
const int32_t clampedEndIndex = Math::Max(clampedStartIndex, Math::Min(endIndex, lineEndIndex));
return this->ResolveTextWidth(clampedStartIndex, clampedEndIndex);}

void TextComponent::SetFocusedState(bool newFocused)
{
    if (this->IsFocusedValue == newFocused)
    {
return;    }
this->IsFocusedValue = newFocused;
this->IsSelectingTextValue = false;
    if (this->IsFocusedValue)
    {
this->CursorPositionValue = static_cast<int32_t>(this->TextValue.size());
this->SelectionAnchorPositionValue = this->CursorPositionValue;
    }
}

void TextComponent::UpdateSelectionRenderOrder()
{
    if (this->SelectionSpriteValue == nullptr)
    {
return;    }
this->SelectionSpriteValue->set_RenderOrder2D(this->ResolveSelectionRenderOrder());
}

void TextComponent::UpdateSelectionVisual()
{
    if (this->SelectionEntityValue == nullptr || this->SelectionSpriteValue == nullptr)
    {
return;    }
    if (!this->get_SelectionEnabled() || this->Font == nullptr || !this->get_HasSelection() || String::IsNullOrEmpty(this->TextValue))
    {
this->SelectionEntityValue->set_LocalPosition(::float3(0.0f, 0.0f, 0.05f));
this->SelectionSpriteValue->set_Size(new int2(0, 0));
this->SelectionSpriteValue->set_FillColor(::byte4(ThemeManager::get_Colors()->get_AccentPrimary().X, ThemeManager::get_Colors()->get_AccentPrimary().Y, ThemeManager::get_Colors()->get_AccentPrimary().Z, 0));
this->SelectionSpriteValue->set_BorderColor(this->SelectionSpriteValue->get_FillColor());
return;    }
const int32_t selectionLineIndex = this->ResolveLineIndexFromTextIndex(this->get_SelectionStart());
const int32_t selectionLineEndIndex = this->ResolveLineEndIndex(selectionLineIndex);
const double selectionStartX = this->ResolveTextWidthInLine(selectionLineIndex, this->ResolveLineStartIndex(selectionLineIndex), this->get_SelectionStart());
const double selectionWidth = this->ResolveTextWidthInLine(selectionLineIndex, this->get_SelectionStart(), Math::Min(this->get_SelectionEnd(), selectionLineEndIndex));
const double lineHeight = Math::Max(static_cast<double>(this->Font->get_LineHeight()), 1.0);
this->SelectionEntityValue->set_LocalPosition(::float3(static_cast<float>(selectionStartX), static_cast<float>((selectionLineIndex * lineHeight)), 0.05f));
this->SelectionSpriteValue->set_Size(([&]() {
auto __ctor_arg_000001C0 = static_cast<int32_t>(Math::Ceiling(selectionWidth));
auto __ctor_arg_000001C1 = static_cast<int32_t>(Math::Ceiling(lineHeight));
return new int2(__ctor_arg_000001C0, __ctor_arg_000001C1);
})());
this->SelectionSpriteValue->set_FillColor(::byte4(ThemeManager::get_Colors()->get_AccentPrimary().X, ThemeManager::get_Colors()->get_AccentPrimary().Y, ThemeManager::get_Colors()->get_AccentPrimary().Z, 96));
this->SelectionSpriteValue->set_BorderColor(this->SelectionSpriteValue->get_FillColor());
}

