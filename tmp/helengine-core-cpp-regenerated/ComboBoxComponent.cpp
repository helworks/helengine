#ifdef DrawText
#undef DrawText
#endif
#include "ComboBoxComponent.hpp"
#include "runtime/native_exceptions.hpp"
#include "ComboBoxComponent.hpp"
#include "runtime/native_list.hpp"
#include "Component.hpp"
#include "Entity.hpp"
#include "ComboBoxUpdateComponent.hpp"
#include "runtime/native_string.hpp"
#include "InputManager.hpp"
#include "Core.hpp"
#include "int2.hpp"
#include "float3.hpp"
#include "system/math.hpp"
#include "FontTightMetrics.hpp"
#include "FontAsset.hpp"
#include "ComboBoxItemVisual.hpp"
#include "ICamera.hpp"
#include "float4.hpp"
#include "GeometryUtils.hpp"
#include "ObjectManager.hpp"
#include "Keys.hpp"
#include "RenderOrder2D.hpp"
#include "RoundedRectComponent.hpp"
#include "ThemeManager.hpp"
#include "InteractableComponent.hpp"
#include "TextComponent.hpp"
#include "PointerInteraction.hpp"
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

bool ComboBoxComponent::get_CanReceiveFocus()
{
return Parent != nullptr && Parent->get_IsHierarchyEnabled() && this->interactable != nullptr;
}

::IFocusGroup* ComboBoxComponent::get_FocusGroup()
{
return this->FocusGroup;
}

void ComboBoxComponent::set_FocusGroup(::IFocusGroup* value)
{
this->FocusGroup = value;
}

::FontAsset* ComboBoxComponent::get_Font()
{
return this->font;}

void ComboBoxComponent::set_Font(::FontAsset* value)
{
    if (value == nullptr)
    {
throw new ArgumentNullException("value");
    }
this->font = value;
this->UpdateLabelText();
this->UpdateLayout();
}

bool ComboBoxComponent::get_HasSelection()
{
return this->selectedIndex >= 0 && this->selectedIndex < this->items->Count();
}

bool ComboBoxComponent::get_IsDefaultTarget()
{
return this->IsDefaultTarget;
}

void ComboBoxComponent::set_IsDefaultTarget(bool value)
{
this->IsDefaultTarget = value;
}

bool ComboBoxComponent::get_IsKeyboardFocused()
{
return this->IsKeyboardFocused;
}

void ComboBoxComponent::set_IsKeyboardFocused(bool value)
{
this->IsKeyboardFocused = value;
}

bool ComboBoxComponent::get_IsOpen()
{
return this->isOpen;}

void ComboBoxComponent::set_IsOpen(bool value)
{
    if (value && this->items->Count() == 0)
    {
this->isOpen = false;
this->UpdateDropdownVisibility();
return;    }
    if (this->isOpen == value)
    {
return;    }
this->isOpen = value;
this->UpdateDropdownVisibility();
}

List<std::string>* ComboBoxComponent::get_Items()
{
return this->items;
}

int32_t ComboBoxComponent::get_SelectedIndex()
{
return this->selectedIndex;}

void ComboBoxComponent::set_SelectedIndex(int32_t value)
{
this->SetSelectedIndexInternal(value, true);
}

std::string ComboBoxComponent::get_SelectedItem()
{
    if (!this->get_HasSelection())
    {
throw new InvalidOperationException("ComboBox has no selected item.");
    }
return (*this->items)[this->selectedIndex];}

::int2 ComboBoxComponent::get_Size()
{
return this->size;}

void ComboBoxComponent::set_Size(::int2 value)
{
    if (value.X <= 0 || value.Y <= 0)
    {
throw ([&]() {
auto __ctor_arg_f66eb3c7 = "value";
auto __ctor_arg_6dab3d7d = "ComboBox size must be positive.";
return new ArgumentOutOfRangeException(__ctor_arg_f66eb3c7, __ctor_arg_6dab3d7d);
})();
    }
this->size = value;
this->itemHeight = this->size.Y;
this->UpdateLayout();
}

int32_t ComboBoxComponent::get_TabIndex()
{
return this->TabIndex;
}

void ComboBoxComponent::set_TabIndex(int32_t value)
{
this->TabIndex = value;
}

void ComboBoxComponent::ActivateFromKey(::Keys key)
{
    if (!this->CanActivateWithKey(key) || this->items->Count() == 0)
    {
return;    }
this->set_IsOpen(!this->isOpen);
}

bool ComboBoxComponent::CanActivateWithKey(::Keys key)
{
return key == Keys::Enter || key == Keys::Space;}

ComboBoxComponent::ComboBoxComponent(::int2 size, ::FontAsset* font, List<std::string>* items, int32_t selectedIndex) : FocusGroup(), IsDefaultTarget(), IsKeyboardFocused(), SelectionChanged(), TabIndex(0), arrowEntity(), arrowText(), background(), backgroundOrder(), font(), hasRenderOrderOverrides(), interactable(), isHovering(), isOpen(), isPressed(), itemHeight(0), itemVisuals(), items(), labelEntity(), labelText(), listBackground(), listBackgroundOrder(), listRoot(), listTextOrder(), selectedIndex(0), size(), textOrder()
{
    if (size.X <= 0 || size.Y <= 0)
    {
throw ([&]() {
auto __ctor_arg_db72eac7 = "size";
auto __ctor_arg_20d03dae = "ComboBox size must be positive.";
return new ArgumentOutOfRangeException(__ctor_arg_db72eac7, __ctor_arg_20d03dae);
})();
    }
else     if (font == nullptr)
    {
throw new ArgumentNullException("font");
    }
else     if (items == nullptr)
    {
throw new ArgumentNullException("items");
    }
this->size = size;
this->font = font;
this->items = new List<std::string>(items->get_Count());
this->itemVisuals = new List<::ComboBoxItemVisual*>(items->get_Count());
this->itemHeight = size.Y;
this->CopyItems(items);
this->selectedIndex = this->ValidateSelectedIndex(this->items->Count(), selectedIndex);
}

void ComboBoxComponent::ComponentAdded(::Entity* entity)
{
Component::ComponentAdded(entity);
    if (!this->hasRenderOrderOverrides)
    {
this->backgroundOrder = RenderOrder2D::PanelSurface;
this->textOrder = RenderOrder2D::PanelForeground;
this->listBackgroundOrder = RenderOrder2D::OverlayBackground;
this->listTextOrder = RenderOrder2D::OverlayForeground;
    }
this->background = new ::RoundedRectComponent();
this->background->set_Size(this->size);
this->background->set_Radius(this->GetCornerRadius(this->size));
this->background->set_BorderThickness(2.0f);
this->background->set_FillColor(ThemeManager::get_Colors()->get_SurfaceInput());
this->background->set_BorderColor(ThemeManager::get_Colors()->get_AccentTertiary());
this->background->set_RenderOrder2D(this->backgroundOrder);
entity->AddComponent(this->background);
this->interactable = new ::InteractableComponent();
this->interactable->set_Size(this->size);
this->interactable->CursorEvent += &ComboBoxComponent::HandleMainCursorEvent;
entity->AddComponent(this->interactable);
    if (entity->get_Children() == nullptr)
    {
entity->InitChildren();
    }
this->labelEntity = new ::Entity();
this->labelEntity->set_LayerMask(entity->get_LayerMask());
this->labelEntity->set_Enabled(true);
this->labelEntity->InitComponents();
entity->AddChild(this->labelEntity);
this->labelText = new ::TextComponent();
this->labelText->set_Font(this->font);
this->labelText->set_Color(ThemeManager::get_Colors()->get_InputForegroundPrimary());
this->labelText->set_RenderOrder2D(this->textOrder);
this->labelEntity->AddComponent(this->labelText);
this->arrowEntity = new ::Entity();
this->arrowEntity->set_LayerMask(entity->get_LayerMask());
this->arrowEntity->set_Enabled(true);
this->arrowEntity->InitComponents();
entity->AddChild(this->arrowEntity);
this->arrowText = new ::TextComponent();
this->arrowText->set_Font(this->font);
this->arrowText->set_Color(ThemeManager::get_Colors()->get_InputForegroundSecondary());
this->arrowText->set_RenderOrder2D(this->textOrder);
this->arrowEntity->AddComponent(this->arrowText);
this->listRoot = new ::Entity();
this->listRoot->set_LayerMask(entity->get_LayerMask());
this->listRoot->InitComponents();
this->listRoot->InitChildren();
entity->AddChild(this->listRoot);
this->listBackground = new ::RoundedRectComponent();
this->listBackground->set_RenderOrder2D(this->listBackgroundOrder);
this->listBackground->set_BorderThickness(1.0f);
this->listBackground->set_FillColor(ThemeManager::get_Colors()->get_SurfacePrimary());
this->listBackground->set_BorderColor(ThemeManager::get_Colors()->get_AccentTertiary());
this->listRoot->AddComponent(this->listBackground);
::ComboBoxUpdateComponent *updateComponent = new ::ComboBoxUpdateComponent(this);
updateComponent->set_UpdateOrder(Core::get_Instance()->get_ObjectManager()->GetUpdateOrderForLayer(1));
entity->AddComponent(updateComponent);
this->EnsureItemVisuals(this->items->Count());
this->UpdateLabelText();
this->UpdateLayout();
this->UpdateDropdownVisibility();
}

void ComboBoxComponent::ComponentRemoved(::Entity* entity)
{
Component::ComponentRemoved(entity);
this->isHovering = false;
this->isPressed = false;
this->ResetItemStates();
this->SetTargetFocused(false);
}

bool ComboBoxComponent::ContainsScreenPoint(::int2 point)
{
    if (Parent == nullptr)
    {
return false;    }
::float3 origin = Parent->get_Position();
return point.X >= origin.X && point.X < origin.X + this->size.X && point.Y >= origin.Y && point.Y < origin.Y + this->size.Y;}

void ComboBoxComponent::ParentEnabledChange(bool newEnabled)
{
Component::ParentEnabledChange(newEnabled);
    if (!newEnabled)
    {
this->isHovering = false;
this->isPressed = false;
this->ResetItemStates();
this->SetTargetFocused(false);
    }
}

void ComboBoxComponent::SetItems(List<std::string>* items, int32_t selectedIndex)
{
    if (items == nullptr)
    {
throw new ArgumentNullException("items");
    }
this->ValidateItems(items);
const int32_t validatedIndex = this->ValidateSelectedIndex(items->get_Count(), selectedIndex);
const bool selectionChanged = this->selectedIndex != validatedIndex;
this->items->Clear();
for (int32_t i = 0; i < items->get_Count(); i++) {
this->items->Add((*items)[i]);
}
this->selectedIndex = validatedIndex;
    if (this->items->Count() == 0 && this->isOpen)
    {
this->isOpen = false;
    }
this->UpdateLabelText();
this->UpdateLayout();
this->UpdateDropdownVisibility();
    if (selectionChanged && this->get_HasSelection() && true)
    {
this->SelectionChanged.Invoke(this->selectedIndex, (*this->items)[this->selectedIndex]);
    }
}

void ComboBoxComponent::SetRenderOrders(uint8_t backgroundOrder, uint8_t textOrder, uint8_t listBackgroundOrder, uint8_t listTextOrder)
{
this->hasRenderOrderOverrides = true;
this->backgroundOrder = backgroundOrder;
this->textOrder = textOrder;
this->listBackgroundOrder = listBackgroundOrder;
this->listTextOrder = listTextOrder;
this->ApplyRenderOrders();
}

void ComboBoxComponent::SetTargetFocused(bool isFocused)
{
this->set_IsKeyboardFocused(isFocused);
    if (!isFocused && this->isOpen)
    {
this->set_IsOpen(false);
    }
this->UpdateMainVisual();
}

void ComboBoxComponent::Update()
{
    if (!this->isOpen || Parent == nullptr || this->listRoot == nullptr)
    {
return;    }
::InputManager *inputManager = Core::get_Instance()->get_InputManager();
    if (!inputManager->WasMouseLeftButtonPressed())
    {
return;    }
::int2 mousePosition = inputManager->GetMousePosition();
    if (this->IsPointerInsideCombo(mousePosition))
    {
return;    }
this->set_IsOpen(false);
}

::Entity* ComboBoxComponent::get_Parent()
{
return this->Component::get_Parent();
}

void ComboBoxComponent::set_Parent(::Entity* value)
{
this->Component::set_Parent(value);
}

std::string ComboBoxComponent::ArrowGlyph = "v";

int32_t ComboBoxComponent::ArrowPaddingX = 8;

int32_t ComboBoxComponent::ListGap = 2;

int32_t ComboBoxComponent::TextPaddingX = 8;

void ComboBoxComponent::ApplyRenderOrders()
{
    if (this->background != nullptr)
    {
this->background->set_RenderOrder2D(this->backgroundOrder);
    }
    if (this->labelText != nullptr)
    {
this->labelText->set_RenderOrder2D(this->textOrder);
    }
    if (this->arrowText != nullptr)
    {
this->arrowText->set_RenderOrder2D(this->textOrder);
    }
    if (this->listBackground != nullptr)
    {
this->listBackground->set_RenderOrder2D(this->listBackgroundOrder);
    }
for (int32_t i = 0; i < this->itemVisuals->Count(); i++) {
::ComboBoxItemVisual *entry = (*this->itemVisuals)[i];
entry->get_Background()->set_RenderOrder2D(this->listBackgroundOrder);
entry->get_Label()->set_RenderOrder2D(this->listTextOrder);
}
}

void ComboBoxComponent::CopyItems(List<std::string>* source)
{
this->ValidateItems(source);
for (int32_t i = 0; i < source->get_Count(); i++) {
this->items->Add((*source)[i]);
}
}

::ComboBoxItemVisual* ComboBoxComponent::CreateItemVisual()
{
::ComboBoxItemVisual *entry = new ::ComboBoxItemVisual(this->font, this->listRoot->get_LayerMask(), this->listBackgroundOrder, this->listTextOrder);
entry->get_Background()->set_FillColor(ThemeManager::get_Colors()->get_SurfaceInput());
entry->get_Background()->set_BorderColor(ThemeManager::get_Colors()->get_AccentTertiary());
entry->get_Label()->set_Color(ThemeManager::get_Colors()->get_InputForegroundPrimary());
return entry;}

void ComboBoxComponent::EnsureItemVisuals(int32_t count)
{
    if (this->listRoot == nullptr)
    {
return;    }
for (int32_t i = this->itemVisuals->Count(); i < count; i++) {
::ComboBoxItemVisual *entry = this->CreateItemVisual();
entry->CursorEvent += &ComboBoxComponent::HandleItemCursorEvent;
this->listRoot->AddChild(entry->get_Root());
this->itemVisuals->Add(entry);
}
}

::ICamera* ComboBoxComponent::FindTopmostCameraAt(int32_t x, int32_t y, uint16_t layerMask)
{
List<::ICamera*> *cameras = Core::get_Instance()->get_ObjectManager()->get_Cameras();
for (int32_t i = cameras->Count() - 1; i >= 0; i--) {
::ICamera *camera = (*cameras)[i];
    if ((camera->get_LayerMask() & layerMask) == 0)
    {
continue;
    }
::float4 vp = camera->get_Viewport();
    if (x >= vp.X && x < vp.X + vp.Z && y >= vp.Y && y < vp.Y + vp.W)
    {
return camera;    }
}
return nullptr;}

float ComboBoxComponent::GetCornerRadius(::int2 size)
{
const double minAxis = Math::Min(size.X, size.Y);
return static_cast<float>((minAxis * 0.15));}

void ComboBoxComponent::HandleItemCursorEvent(::ComboBoxItemVisual* entry, ::int2 relPos, ::int2 delta, ::PointerInteraction state)
{
switch (state) {
case PointerInteraction::Hover: {
entry->set_IsHovering(true);
this->UpdateItemVisualState(entry, entry->get_Index() == this->selectedIndex);
break;
}
case PointerInteraction::Press: {
entry->set_IsPressed(true);
this->UpdateItemVisualState(entry, entry->get_Index() == this->selectedIndex);
break;
}
case PointerInteraction::Release: {
const bool shouldSelect = entry->get_IsPressed() && entry->get_IsHovering();
entry->set_IsPressed(false);
this->UpdateItemVisualState(entry, entry->get_Index() == this->selectedIndex);
    if (shouldSelect)
    {
this->SetSelectedIndexInternal(entry->get_Index(), true);
this->set_IsOpen(false);
    }
break;
}
case PointerInteraction::Leave: {
entry->set_IsHovering(false);
entry->set_IsPressed(false);
this->UpdateItemVisualState(entry, entry->get_Index() == this->selectedIndex);
break;
}
case PointerInteraction::None: {
break;
}
}

}

void ComboBoxComponent::HandleMainCursorEvent(::int2 relPos, ::int2 delta, ::PointerInteraction state)
{
switch (state) {
case PointerInteraction::Hover: {
    if (!this->isHovering)
    {
this->isHovering = true;
this->UpdateMainVisual();
    }
break;
}
case PointerInteraction::Press: {
this->isPressed = true;
this->UpdateMainVisual();
break;
}
case PointerInteraction::Release: {
const bool shouldToggle = this->isPressed && this->isHovering;
this->isPressed = false;
this->UpdateMainVisual();
    if (shouldToggle && this->items->Count() > 0)
    {
this->set_IsOpen(!this->isOpen);
    }
break;
}
case PointerInteraction::Leave: {
    if (this->isHovering || this->isPressed)
    {
this->isHovering = false;
this->isPressed = false;
this->UpdateMainVisual();
    }
break;
}
case PointerInteraction::None: {
break;
}
}

}

void ComboBoxComponent::HideItemVisuals()
{
for (int32_t i = 0; i < this->itemVisuals->Count(); i++) {
::ComboBoxItemVisual *entry = (*this->itemVisuals)[i];
entry->get_Root()->set_Enabled(false);
entry->get_LabelHost()->set_Enabled(false);
entry->get_Label()->set_Text(String::Empty);
entry->get_Label()->set_Size(::int2(0, 0));
}
}

bool ComboBoxComponent::IsPointerInsideCombo(::int2 mousePosition)
{
::ICamera *camera = this->FindTopmostCameraAt(mousePosition.X, mousePosition.Y, Parent->get_LayerMask());
    if (camera == nullptr)
    {
return false;    }
::float4 viewport = camera->get_Viewport();
const double localX = mousePosition.X - viewport.X;
const double localY = mousePosition.Y - viewport.Y;
::float3 origin = Parent->get_Position();
    if (GeometryUtils::IsPointInsideRect(localX, localY, origin, this->size.X, this->size.Y))
    {
return true;    }
    if (!this->isOpen)
    {
return false;    }
const int32_t listHeight = this->itemHeight * this->items->Count();
    if (listHeight <= 0)
    {
return false;    }
::float3 listPosition = this->listRoot->get_Position();
return GeometryUtils::IsPointInsideRect(localX, localY, listPosition, this->size.X, listHeight);}

void ComboBoxComponent::ResetItemStates()
{
for (int32_t i = 0; i < this->itemVisuals->Count(); i++) {
::ComboBoxItemVisual *entry = (*this->itemVisuals)[i];
entry->set_IsHovering(false);
entry->set_IsPressed(false);
}
}

void ComboBoxComponent::SetSelectedIndexInternal(int32_t index, bool raiseEvent)
{
const int32_t validated = this->ValidateSelectedIndex(this->items->Count(), index);
    if (this->selectedIndex == validated)
    {
return;    }
this->selectedIndex = validated;
this->UpdateLabelText();
this->UpdateAllItemStates();
    if (raiseEvent && this->get_HasSelection() && true)
    {
this->SelectionChanged.Invoke(this->selectedIndex, (*this->items)[this->selectedIndex]);
    }
}

void ComboBoxComponent::UpdateAllItemStates()
{
const int32_t count = Math::Min(this->items->Count(), this->itemVisuals->Count());
for (int32_t i = 0; i < count; i++) {
this->UpdateItemVisualState((*this->itemVisuals)[i], i == this->selectedIndex);
}
}

void ComboBoxComponent::UpdateDropdownVisibility()
{
    if (this->listRoot == nullptr)
    {
return;    }
const bool shouldShow = this->isOpen && this->items->Count() > 0;
this->listRoot->set_Enabled(shouldShow);
this->UpdateListLayout();
    if (!shouldShow)
    {
this->HideItemVisuals();
this->ResetItemStates();
    }
this->UpdateMainVisual();
}

void ComboBoxComponent::UpdateItemVisualState(::ComboBoxItemVisual* entry, bool isSelected)
{
    if (entry->get_IsPressed())
    {
entry->get_Background()->set_FillColor(ThemeManager::get_Colors()->get_AccentSecondary());
    }
else     if (entry->get_IsHovering())
    {
entry->get_Background()->set_FillColor(ThemeManager::get_Colors()->get_AccentPrimary());
    }
else     if (isSelected)
    {
entry->get_Background()->set_FillColor(ThemeManager::get_Colors()->get_AccentTertiary());
    }
else {
entry->get_Background()->set_FillColor(ThemeManager::get_Colors()->get_SurfaceInput());
}
}

void ComboBoxComponent::UpdateLabelLayout()
{
    if (this->labelEntity == nullptr || this->labelText == nullptr || this->arrowEntity == nullptr || this->arrowText == nullptr || this->font == nullptr)
    {
return;    }
const double lineHeight = Math::Max(static_cast<double>(this->font->get_LineHeight()), 1.0);
const double labelY = Math::Round((this->size.Y - lineHeight) / 2.0, MidpointRounding::AwayFromZero);
::FontTightMetrics labelMetrics = this->font->MeasureTight(this->labelText->get_Text());
const int32_t labelWidth = static_cast<int32_t>(Math::Ceiling(labelMetrics.Width));
const int32_t labelHeight = static_cast<int32_t>(Math::Ceiling(Math::Max(static_cast<double>(labelMetrics.get_Height()), 1.0)));
this->labelText->set_Size(::int2(labelWidth, labelHeight));
this->labelEntity->set_Position(::float3(TextPaddingX, static_cast<float>(labelY), 0.1f));
::FontTightMetrics arrowMetrics = this->font->MeasureTight(ArrowGlyph);
const int32_t arrowWidth = static_cast<int32_t>(Math::Ceiling(arrowMetrics.Width));
const int32_t arrowHeight = static_cast<int32_t>(Math::Ceiling(Math::Max(static_cast<double>(arrowMetrics.get_Height()), 1.0)));
this->arrowText->set_Size(::int2(arrowWidth, arrowHeight));
double arrowX = this->size.X - ArrowPaddingX - arrowMetrics.Width;
    if (arrowX < TextPaddingX)
    {
arrowX = TextPaddingX;
    }
arrowX = Math::Round(arrowX, MidpointRounding::AwayFromZero);
this->arrowEntity->set_Position(::float3(static_cast<float>(arrowX), static_cast<float>(labelY), 0.1f));
}

void ComboBoxComponent::UpdateLabelText()
{
    if (this->labelText == nullptr || this->arrowText == nullptr)
    {
return;    }
const std::string displayText = this->get_HasSelection() ? (*items)[selectedIndex] : String::Empty;
this->labelText->set_Text(displayText);
this->labelText->set_Color(this->get_HasSelection() ? ThemeManager::get_Colors()->get_InputForegroundPrimary() : ThemeManager::get_Colors()->get_InputForegroundSecondary());
this->arrowText->set_Text(ArrowGlyph);
this->arrowText->set_Color(ThemeManager::get_Colors()->get_InputForegroundSecondary());
this->UpdateLabelLayout();
}

void ComboBoxComponent::UpdateLayout()
{
this->UpdateMainLayout();
this->UpdateListLayout();
}

void ComboBoxComponent::UpdateListLayout()
{
    if (this->listRoot == nullptr || this->listBackground == nullptr)
    {
return;    }
this->listRoot->set_Position(::float3(0.0f, this->size.Y + ListGap, 0.2f));
int32_t listHeight = this->itemHeight * this->items->Count();
    if (listHeight <= 0)
    {
listHeight = 1;
    }
this->listBackground->set_Size(::int2(this->size.X, listHeight));
    if (this->background != nullptr)
    {
this->listBackground->set_Radius(this->background->get_Radius());
    }
this->EnsureItemVisuals(this->items->Count());
const double lineHeight = Math::Max(static_cast<double>(this->font->get_LineHeight()), 1.0);
const bool shouldShow = this->isOpen && this->items->Count() > 0;
for (int32_t i = 0; i < this->itemVisuals->Count(); i++) {
::ComboBoxItemVisual *entry = (*this->itemVisuals)[i];
const bool isActive = i < this->items->Count();
const bool isVisible = isActive && shouldShow;
entry->get_Root()->set_Enabled(isVisible);
entry->get_LabelHost()->set_Enabled(isVisible);
    if (!isActive)
    {
entry->get_Label()->set_Text(String::Empty);
entry->get_Label()->set_Size(::int2(0, 0));
continue;
    }
    if (!shouldShow)
    {
entry->get_Label()->set_Text(String::Empty);
entry->get_Label()->set_Size(::int2(0, 0));
continue;
    }
entry->set_Index(i);
entry->get_Root()->set_Position(::float3(0.0f, this->itemHeight * i, 0.1f));
entry->get_Background()->set_Size(::int2(this->size.X, this->itemHeight));
entry->get_Background()->set_Radius(0.0f);
entry->get_Background()->set_BorderColor(ThemeManager::get_Colors()->get_AccentTertiary());
entry->get_Interactable()->set_Size(::int2(this->size.X, this->itemHeight));
const std::string itemText = (*this->items)[i];
entry->get_Label()->set_Text(itemText);
entry->get_Label()->set_Font(this->font);
entry->get_Label()->set_Color(ThemeManager::get_Colors()->get_InputForegroundPrimary());
::FontTightMetrics itemMetrics = this->font->MeasureTight(itemText);
entry->get_Label()->set_Size(([&]() {
auto __ctor_arg_c36d9a48 = static_cast<int32_t>(Math::Ceiling(itemMetrics.Width));
auto __ctor_arg_97801900 = static_cast<int32_t>(Math::Ceiling(Math::Max(static_cast<double>(itemMetrics.get_Height()), 1.0)));
return ::int2(__ctor_arg_c36d9a48, __ctor_arg_97801900);
})());
const double textY = Math::Round((this->itemHeight - lineHeight) / 2.0, MidpointRounding::AwayFromZero);
entry->get_LabelHost()->set_Position(::float3(TextPaddingX, static_cast<float>(textY), 0.1f));
this->UpdateItemVisualState(entry, i == this->selectedIndex);
}
}

void ComboBoxComponent::UpdateMainLayout()
{
    if (this->background == nullptr || this->interactable == nullptr)
    {
return;    }
this->background->set_Size(this->size);
this->background->set_Radius(this->GetCornerRadius(this->size));
this->interactable->set_Size(this->size);
this->UpdateLabelLayout();
}

void ComboBoxComponent::UpdateMainVisual()
{
    if (this->background == nullptr)
    {
return;    }
this->background->set_BorderColor(this->IsKeyboardFocused ? ThemeManager::get_Colors()->get_AccentPrimary() : ThemeManager::get_Colors()->get_AccentTertiary());
    if (this->listBackground != nullptr)
    {
this->listBackground->set_BorderColor(this->background->get_BorderColor());
    }
    if (this->isPressed || this->isOpen)
    {
this->background->set_FillColor(ThemeManager::get_Colors()->get_AccentSecondary());
    }
else     if (this->isHovering)
    {
this->background->set_FillColor(ThemeManager::get_Colors()->get_AccentPrimary());
    }
else {
this->background->set_FillColor(ThemeManager::get_Colors()->get_SurfaceInput());
}
}

void ComboBoxComponent::ValidateItems(List<std::string>* items)
{
for (int32_t i = 0; i < items->get_Count(); i++) {
    if (String::IsNullOrEmpty((*items)[i]))
    {
throw ([&]() {
auto __ctor_arg_b832fc58 = "ComboBox items must not contain null entries.";
auto __ctor_arg_e968f397 = "items";
return new ArgumentException(__ctor_arg_b832fc58, __ctor_arg_e968f397);
})();
    }
}
}

int32_t ComboBoxComponent::ValidateSelectedIndex(int32_t itemCount, int32_t index)
{
    if (index < -1 || index >= itemCount)
    {
throw ([&]() {
auto __ctor_arg_68a9d704 = "index";
auto __ctor_arg_0145672b = "SelectedIndex must be -1 or within the item range.";
return new ArgumentOutOfRangeException(__ctor_arg_68a9d704, __ctor_arg_0145672b);
})();
    }
return index;}

