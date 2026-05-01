#ifdef DrawText
#undef DrawText
#endif
#include "TextBoxComponent.hpp"
#include "runtime/native_exceptions.hpp"
#include "Component.hpp"
#include "RenderOrder2D.hpp"
#include "Entity.hpp"
#include "TextBoxUpdateComponent.hpp"
#include "InputManager.hpp"
#include "Core.hpp"
#include "runtime/native_string.hpp"
#include "system/math.hpp"
#include "FontTightMetrics.hpp"
#include "FontAsset.hpp"
#include "float3.hpp"
#include "Keys.hpp"
#include "RoundedRectComponent.hpp"
#include "ThemeManager.hpp"
#include "TextComponent.hpp"
#include "byte4.hpp"
#include "InteractableComponent.hpp"
#include "PointerCursorKind.hpp"
#include "PointerInteraction.hpp"
#include "int2.hpp"
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

bool TextBoxComponent::get_CanReceiveFocus()
{
return Parent != nullptr && Parent->get_IsHierarchyEnabled() && this->interactableComponent != nullptr;
}

float TextBoxComponent::get_CurrentShakeOffsetX()
{
return this->currentShakeOffsetX;
}

::IFocusGroup* TextBoxComponent::get_FocusGroup()
{
return this->FocusGroup;
}

void TextBoxComponent::set_FocusGroup(::IFocusGroup* value)
{
this->FocusGroup = value;
}

::FontAsset* TextBoxComponent::get_Font()
{
return this->font;}

void TextBoxComponent::set_Font(::FontAsset* value)
{
this->font = value;
    if (this->textComponent != nullptr)
    {
this->textComponent->set_Font(this->font);
    }
this->UpdateTextLayout();
}

bool TextBoxComponent::get_IsDefaultTarget()
{
return this->IsDefaultTarget;
}

void TextBoxComponent::set_IsDefaultTarget(bool value)
{
this->IsDefaultTarget = value;
}

bool TextBoxComponent::get_IsFocused()
{
return this->isFocused;}

void TextBoxComponent::set_IsFocused(bool value)
{
this->SetFocusedState(value, true);
}

std::string TextBoxComponent::get_Placeholder()
{
return this->placeholder;}

void TextBoxComponent::set_Placeholder(std::string value)
{
this->placeholder = value;
this->UpdateTextDisplay();
}

::int2 TextBoxComponent::get_Size()
{
return this->size;}

void TextBoxComponent::set_Size(::int2 value)
{
this->size = value;
    if (this->backgroundSprite != nullptr)
    {
this->backgroundSprite->set_Size(this->size);
    }
    if (this->interactableComponent != nullptr)
    {
this->interactableComponent->set_Size(this->size);
    }
this->UpdateTextLayout();
}

int32_t TextBoxComponent::get_TabIndex()
{
return this->TabIndex;
}

void TextBoxComponent::set_TabIndex(int32_t value)
{
this->TabIndex = value;
}

std::string TextBoxComponent::get_Text()
{
return this->text;}

void TextBoxComponent::set_Text(std::string value)
{
    if (this->text == (value))
    {
return;    }
this->text = value;
this->cursorPosition = Math::Max(0, Math::Min(this->cursorPosition, static_cast<int32_t>(this->text.size())));
this->UpdateTextDisplay();
this->TextChanged.Invoke(this);
}

void TextBoxComponent::ActivateFromKey(::Keys key)
{
    if (!this->CanActivateWithKey(key) || !this->isFocused)
    {
return;    }
this->set_IsFocused(false);
}

bool TextBoxComponent::CanActivateWithKey(::Keys key)
{
return key == Keys::Enter;}

void TextBoxComponent::ComponentAdded(::Entity* entity)
{
Component::ComponentAdded(entity);
uint8_t backgroundOrder = RenderOrder2D::PanelSurface;
uint8_t textOrder = RenderOrder2D::PanelForeground;
    if (this->hasRenderOrderOverrides)
    {
backgroundOrder = this->backgroundRenderOrder;
textOrder = this->textRenderOrder;
    }
this->backgroundSprite = new ::RoundedRectComponent();
this->backgroundSprite->set_Size(this->size);
this->backgroundSprite->set_Radius(MathF::Min(this->size.X, this->size.Y) * 0.15f);
this->backgroundSprite->set_BorderThickness(2.0f);
this->backgroundSprite->set_FillColor(ThemeManager::get_Colors()->get_SurfaceInput());
this->backgroundSprite->set_BorderColor(ThemeManager::get_Colors()->get_AccentTertiary());
this->backgroundSprite->set_RenderOrder2D(backgroundOrder);
entity->AddComponent(this->backgroundSprite);
this->textEntity = new ::Entity();
this->textEntity->set_LayerMask(entity->get_LayerMask());
this->textEntity->set_Enabled(true);
this->textEntity->InitComponents();
    if (entity->get_Children() == nullptr)
    {
entity->InitChildren();
    }
entity->AddChild(this->textEntity);
this->textComponent = new ::TextComponent();
this->textComponent->set_Font(this->font);
this->textComponent->set_Color(::byte4(255, 255, 255, 255));
this->textComponent->set_RenderOrder2D(textOrder);
this->textEntity->AddComponent(this->textComponent);
this->interactableComponent = new ::InteractableComponent();
this->interactableComponent->set_HoverCursor(PointerCursorKind::Text);
this->interactableComponent->set_Size(this->size);
this->interactableComponent->CursorEvent += &TextBoxComponent::OnCursorEvent;
entity->AddComponent(this->interactableComponent);
::TextBoxUpdateComponent *updateComponent = new ::TextBoxUpdateComponent(this);
updateComponent->set_UpdateOrder(Core::get_Instance()->get_ObjectManager()->GetUpdateOrderForLayer(1));
entity->AddComponent(updateComponent);
this->UpdateTextDisplay();
this->UpdateFocusVisual();
}

void TextBoxComponent::ComponentRemoved(::Entity* entity)
{
Component::ComponentRemoved(entity);
this->SetFocusedState(false, false);
}

bool TextBoxComponent::ContainsScreenPoint(::int2 point)
{
    if (Parent == nullptr)
    {
return false;    }
::float3 worldPosition = Parent->get_Position();
return point.X >= worldPosition.X && point.X < worldPosition.X + this->size.X && point.Y >= worldPosition.Y && point.Y < worldPosition.Y + this->size.Y;}

void TextBoxComponent::ParentEnabledChange(bool newEnabled)
{
Component::ParentEnabledChange(newEnabled);
    if (!newEnabled && this->isFocused)
    {
this->set_IsFocused(false);
    }
    if (this->textEntity != nullptr)
    {
this->textEntity->set_Enabled(newEnabled);
    }
}

void TextBoxComponent::SetInvalidState(bool isInvalid)
{
this->isInvalid = isInvalid;
this->UpdateFocusVisual();
}

void TextBoxComponent::SetRenderOrders(uint8_t backgroundOrder, uint8_t textOrder)
{
this->hasRenderOrderOverrides = true;
this->backgroundRenderOrder = backgroundOrder;
this->textRenderOrder = textOrder;
    if (this->backgroundSprite != nullptr)
    {
this->backgroundSprite->set_RenderOrder2D(backgroundOrder);
    }
    if (this->textComponent != nullptr)
    {
this->textComponent->set_RenderOrder2D(textOrder);
    }
}

void TextBoxComponent::SetTargetFocused(bool isFocused)
{
this->set_IsFocused(isFocused);
}

TextBoxComponent::TextBoxComponent(::int2 size, ::FontAsset* font, std::string placeholder) : FocusChanged(), FocusGroup(), IsDefaultTarget(), Submitted(), TabIndex(0), TextChanged(), backgroundRenderOrder(), backgroundSprite(), currentShakeOffsetX(), cursorPosition(0), cursorVisible(true), font(), hasRenderOrderOverrides(), interactableComponent(), isFocused(), isInvalid(), isShakeActive(), lastCursorBlink(DateTime::Now()), placeholder(""), shakeBaseLocalPosition(), shakeElapsedSeconds(), size(), text(""), textComponent(), textEntity(), textRenderOrder()
{
this->size = size;
this->font = font;
this->placeholder = placeholder;
}

void TextBoxComponent::TriggerInvalidShake()
{
    if (Parent == nullptr)
    {
throw new InvalidOperationException("Text boxes must be attached to an entity before they can animate invalid-input feedback.");
    }
    if (this->isShakeActive)
    {
Parent->set_LocalPosition(this->shakeBaseLocalPosition);
this->currentShakeOffsetX = 0.0f;
    }
this->shakeBaseLocalPosition = Parent->get_LocalPosition();
this->shakeElapsedSeconds = 0.0f;
this->isShakeActive = true;
}

void TextBoxComponent::Update()
{
this->UpdateShakeAnimation();
    if (!this->isFocused)
    {
return;    }
    if ((DateTime::Now() - this->lastCursorBlink).TotalMilliseconds > 500)
    {
this->cursorVisible = !this->cursorVisible;
this->lastCursorBlink = DateTime::Now();
this->UpdateTextDisplay();
    }
::InputManager *inputManager = Core::get_Instance()->get_InputManager();
const bool isShiftPressed = inputManager->IsKeyDown(Keys::LeftShift) || inputManager->IsKeyDown(Keys::RightShift);
for (int32_t i = 0; i < 255; i++) {
::Keys key = static_cast<Keys>(i);
    if (inputManager->WasKeyPressed(key))
    {
this->HandleKeyPress(key, isShiftPressed);
    }
}
}

::Entity* TextBoxComponent::get_Parent()
{
return this->Component::get_Parent();
}

void TextBoxComponent::set_Parent(::Entity* value)
{
this->Component::set_Parent(value);
}

float TextBoxComponent::EffectFrameDeltaSeconds = 1.0f / 60.0f;

float TextBoxComponent::ShakeAmplitudePixels = 10.0f;

float TextBoxComponent::ShakeDurationSeconds = 0.3f;

float TextBoxComponent::ShakeFrequencyHz = 16.0f;

int32_t TextBoxComponent::TextPaddingX = 8;

::TextBoxComponent* TextBoxComponent::focusedTextBox;

void TextBoxComponent::HandleKeyPress(::Keys key, bool isShiftPressed)
{
switch (key) {
case Keys::Back: {
    if (this->cursorPosition > 0)
    {
this->set_Text(String::Remove(this->text, this->cursorPosition - 1, 1));
this->cursorPosition--;
    }
break;
}
case Keys::Delete: {
    if (this->cursorPosition < static_cast<int32_t>(this->text.size()))
    {
this->set_Text(String::Remove(this->text, this->cursorPosition, 1));
    }
break;
}
case Keys::Left: {
this->cursorPosition = Math::Max(0, this->cursorPosition - 1);
this->UpdateTextDisplay();
break;
}
case Keys::Right: {
this->cursorPosition = Math::Min(static_cast<int32_t>(this->text.size()), this->cursorPosition + 1);
this->UpdateTextDisplay();
break;
}
case Keys::Home: {
this->cursorPosition = 0;
this->UpdateTextDisplay();
break;
}
case Keys::End: {
this->cursorPosition = static_cast<int32_t>(this->text.size());
this->UpdateTextDisplay();
break;
}
case Keys::Enter: {
this->set_IsFocused(false);
break;
}
default:  {
const char character = this->KeyToChar(key, isShiftPressed);
    if (character != '\0')
    {
const int32_t insertionIndex = Math::Max(0, Math::Min(this->cursorPosition, static_cast<int32_t>(this->text.size())));
this->set_Text(String::Insert(this->text, insertionIndex, std::string(1, character)));
this->cursorPosition = insertionIndex + 1;
    }
break;
}
}

}

char TextBoxComponent::KeyToChar(::Keys key, bool isShiftPressed)
{
    if (key >= Keys::A && key <= Keys::Z)
    {
const char baseChar = static_cast<char>(('a' + (static_cast<int32_t>(key) - static_cast<int32_t>(Keys::A))));
return isShiftPressed ? String::ToUpper(baseChar) : baseChar;    }
switch (key) {
case Keys::D0: {
return isShiftPressed ? ')' : '0';}
case Keys::D1: {
return isShiftPressed ? '!' : '1';}
case Keys::D2: {
return isShiftPressed ? '@' : '2';}
case Keys::D3: {
return isShiftPressed ? '#' : '3';}
case Keys::D4: {
return isShiftPressed ? '$' : '4';}
case Keys::D5: {
return isShiftPressed ? '%' : '5';}
case Keys::D6: {
return isShiftPressed ? '^' : '6';}
case Keys::D7: {
return isShiftPressed ? '&' : '7';}
case Keys::D8: {
return isShiftPressed ? '*' : '8';}
case Keys::D9: {
return isShiftPressed ? '(' : '9';}
case Keys::Space: {
return ' ';}
case Keys::OemPeriod: {
return isShiftPressed ? '>' : '.';}
case Keys::OemComma: {
return isShiftPressed ? '<' : ',';}
case Keys::OemMinus: {
return isShiftPressed ? '_' : '-';}
case Keys::OemPlus: {
return isShiftPressed ? '+' : '=';}
case Keys::OemQuestion: {
return isShiftPressed ? '?' : '/';}
case Keys::OemSemicolon: {
return isShiftPressed ? ':' : ';';}
case Keys::OemQuotes: {
return isShiftPressed ? '"' : '\'';}
case Keys::OemOpenBrackets: {
return isShiftPressed ? '{' : '[';}
case Keys::OemCloseBrackets: {
return isShiftPressed ? '}' : ']';}
case Keys::OemPipe: {
return isShiftPressed ? '|' : '\\';}
default:  {
return '\0';}
}

}

void TextBoxComponent::OnCursorEvent(::int2 relPos, ::int2 delta, ::PointerInteraction state)
{
    if (state == PointerInteraction::Press)
    {
    if (focusedTextBox != nullptr && focusedTextBox != this)
    {
focusedTextBox->set_IsFocused(false);
    }
this->set_IsFocused(true);
this->cursorPosition = static_cast<int32_t>(this->text.size());
    }
}

void TextBoxComponent::SetFocusedState(bool value, bool submitOnBlur)
{
    if (this->isFocused == value)
    {
this->UpdateFocusVisual();
return;    }
this->isFocused = value;
    if (this->isFocused)
    {
this->cursorPosition = static_cast<int32_t>(this->text.size());
focusedTextBox = this;
    }
else     if (focusedTextBox == this)
    {
focusedTextBox = nullptr;
    }
this->cursorVisible = true;
this->UpdateTextDisplay();
this->UpdateFocusVisual();
this->FocusChanged.Invoke(this, isFocused);
    if (!this->isFocused && submitOnBlur)
    {
this->Submitted.Invoke(this);
    }
}

void TextBoxComponent::UpdateFocusVisual()
{
    if (this->backgroundSprite == nullptr)
    {
return;    }
    if (this->isInvalid)
    {
this->backgroundSprite->set_BorderColor(ThemeManager::get_Colors()->get_StateDanger());
return;    }
this->backgroundSprite->set_BorderColor(this->isFocused ? ThemeManager::get_Colors()->get_AccentPrimary() : ThemeManager::get_Colors()->get_AccentTertiary());
}

void TextBoxComponent::UpdateShakeAnimation()
{
    if (!this->isShakeActive || Parent == nullptr)
    {
return;    }
this->shakeElapsedSeconds += EffectFrameDeltaSeconds;
    if (this->shakeElapsedSeconds >= ShakeDurationSeconds)
    {
Parent->set_LocalPosition(this->shakeBaseLocalPosition);
this->currentShakeOffsetX = 0.0f;
this->isShakeActive = false;
return;    }
const double progress = this->shakeElapsedSeconds / ShakeDurationSeconds;
const double amplitude = ShakeAmplitudePixels * (1.0 - progress);
const double angle = this->shakeElapsedSeconds * ShakeFrequencyHz * Math::PI * 2.0;
const double offset = Math::Sin(angle) * amplitude;
this->currentShakeOffsetX = static_cast<float>(offset);
Parent->set_LocalPosition(::float3(this->shakeBaseLocalPosition.X + this->currentShakeOffsetX, this->shakeBaseLocalPosition.Y, this->shakeBaseLocalPosition.Z));
}

void TextBoxComponent::UpdateTextDisplay()
{
    if (this->textComponent == nullptr)
    {
return;    }
const bool showPlaceholder = String::IsNullOrEmpty(this->text) && !this->isFocused;
std::string displayText = showPlaceholder ? placeholder : text;
    if (this->isFocused && this->cursorVisible)
    {
const int32_t cursorIndex = Math::Max(0, Math::Min(this->cursorPosition, static_cast<int32_t>(displayText.size())));
displayText = String::Insert(displayText, cursorIndex, "|");
    }
this->textComponent->set_Text(displayText);
    if (showPlaceholder)
    {
this->textComponent->set_Color(::byte4(150, 150, 150, 255));
    }
else {
this->textComponent->set_Color(::byte4(255, 255, 255, 255));
}
this->UpdateTextLayout();
}

void TextBoxComponent::UpdateTextLayout()
{
    if (this->textEntity == nullptr || this->textComponent == nullptr || this->font == nullptr)
    {
return;    }
const double lineHeight = Math::Max(static_cast<double>(this->font->get_LineHeight()), 1.0);
const double textY = Math::Round((this->size.Y - lineHeight) / 2.0, MidpointRounding::AwayFromZero);
::FontTightMetrics textMetrics = this->font->MeasureTight(this->textComponent->get_Text());
this->textEntity->set_Position(::float3(TextPaddingX, static_cast<float>(textY), 0.1f));
this->textComponent->set_Size(([&]() {
auto __ctor_arg_ab9a1a2a = static_cast<int32_t>(Math::Ceiling(textMetrics.Width));
auto __ctor_arg_16fed2ec = static_cast<int32_t>(Math::Ceiling(lineHeight));
return ::int2(__ctor_arg_ab9a1a2a, __ctor_arg_16fed2ec);
})());
}

