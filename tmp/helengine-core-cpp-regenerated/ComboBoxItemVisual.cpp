#ifdef DrawText
#undef DrawText
#endif
#include "ComboBoxItemVisual.hpp"
#include "Entity.hpp"
#include "RoundedRectComponent.hpp"
#include "InteractableComponent.hpp"
#include "TextComponent.hpp"
#include "runtime/array.hpp"
#include "runtime/finally.hpp"
#include "runtime/native_dictionary.hpp"
#include "runtime/native_disposable.hpp"
#include "runtime/native_enum.hpp"
#include "runtime/native_event.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_nullable.hpp"
#include "runtime/native_span.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_tuple.hpp"
#include "runtime/native_type.hpp"
#include "system/bit_converter.hpp"
#include "system/io/memory-stream.hpp"
#include "system/io/stream.hpp"
#include "system/math.hpp"
#include "system/text/encoding.hpp"

::RoundedRectComponent* ComboBoxItemVisual::get_Background()
{
return this->Background;
}

int32_t ComboBoxItemVisual::get_Index()
{
return this->Index;
}

void ComboBoxItemVisual::set_Index(int32_t value)
{
this->Index = value;
}

::InteractableComponent* ComboBoxItemVisual::get_Interactable()
{
return this->Interactable;
}

bool ComboBoxItemVisual::get_IsHovering()
{
return this->IsHovering;
}

void ComboBoxItemVisual::set_IsHovering(bool value)
{
this->IsHovering = value;
}

bool ComboBoxItemVisual::get_IsPressed()
{
return this->IsPressed;
}

void ComboBoxItemVisual::set_IsPressed(bool value)
{
this->IsPressed = value;
}

::TextComponent* ComboBoxItemVisual::get_Label()
{
return this->Label;
}

::Entity* ComboBoxItemVisual::get_LabelHost()
{
return this->LabelHost;
}

::Entity* ComboBoxItemVisual::get_Root()
{
return this->Root;
}

ComboBoxItemVisual::ComboBoxItemVisual(::FontAsset* font, uint16_t layerMask, uint8_t backgroundOrder, uint8_t textOrder) : Background(), CursorEvent(), Index(0), Interactable(), IsHovering(), IsPressed(), Label(), LabelHost(), Root()
{
    if (font == nullptr)
    {
throw new ArgumentNullException("font");
    }
this->Root = new ::Entity();
this->Root->set_LayerMask(layerMask);
this->Root->set_Enabled(true);
this->Root->InitComponents();
this->Root->InitChildren();
this->Background = new ::RoundedRectComponent();
this->Background->set_RenderOrder2D(backgroundOrder);
this->Background->set_BorderThickness(1.0f);
this->Root->AddComponent(this->Background);
this->Interactable = new ::InteractableComponent();
this->Interactable->CursorEvent += &ComboBoxItemVisual::HandleCursorEvent;
this->Root->AddComponent(this->Interactable);
this->LabelHost = new ::Entity();
this->LabelHost->set_LayerMask(layerMask);
this->LabelHost->set_Enabled(true);
this->LabelHost->InitComponents();
this->Root->AddChild(this->LabelHost);
this->Label = new ::TextComponent();
this->Label->set_Font(font);
this->Label->set_RenderOrder2D(textOrder);
this->LabelHost->AddComponent(this->Label);
}

void ComboBoxItemVisual::HandleCursorEvent(::int2 relPos, ::int2 delta, ::PointerInteraction state)
{
    if (true)
    {
this->CursorEvent.Invoke(this, relPos, delta, state);
    }
}

