#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class Component;
class IFocusTarget;
class Entity;
class ComboBoxUpdateComponent;
class InputManager;
class Core;
class int2;
class float3;
class FontTightMetrics;
class FontAsset;
class ComboBoxItemVisual;
class ICamera;
class float4;
class GeometryUtils;
class ObjectManager;
class IFocusGroup;
class TextComponent;
class RoundedRectComponent;
class InteractableComponent;

#include "Component.hpp"
#include "IFocusTarget.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_list.hpp"
#include "Component.hpp"
#include "Entity.hpp"
#include "ComboBoxUpdateComponent.hpp"
#include "ComboBoxUpdateComponent.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_string.hpp"
#include "InputManager.hpp"
#include "Core.hpp"
#include "Core.hpp"
#include "int2.hpp"
#include "InputManager.hpp"
#include "float3.hpp"
#include "system/math.hpp"
#include "system/math.hpp"
#include "FontTightMetrics.hpp"
#include "FontAsset.hpp"
#include "ComboBoxItemVisual.hpp"
#include "ComboBoxItemVisual.hpp"
#include "ICamera.hpp"
#include "float4.hpp"
#include "ICamera.hpp"
#include "GeometryUtils.hpp"
#include "GeometryUtils.hpp"
#include "runtime/native_list.hpp"
#include "ObjectManager.hpp"
#include "IFocusGroup.hpp"
#include "FontAsset.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_event.hpp"
#include "Entity.hpp"
#include "TextComponent.hpp"
#include "RoundedRectComponent.hpp"
#include "InteractableComponent.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_list.hpp"
#include "Keys.hpp"
#include "PointerInteraction.hpp"

class ComboBoxComponent : public Component, public IFocusTarget
{
public:
    bool get_CanReceiveFocus();

    ::IFocusGroup* FocusGroup;

    ::IFocusGroup* get_FocusGroup();
    void set_FocusGroup(::IFocusGroup* value);

    ::FontAsset* get_Font();

    void set_Font(::FontAsset* value);

    bool get_HasSelection();

    bool IsDefaultTarget;

    bool get_IsDefaultTarget();
    void set_IsDefaultTarget(bool value);

    bool IsKeyboardFocused;

    bool get_IsKeyboardFocused();
    void set_IsKeyboardFocused(bool value);

    bool get_IsOpen();

    void set_IsOpen(bool value);

    List<std::string>* get_Items();

    int32_t get_SelectedIndex();

    void set_SelectedIndex(int32_t value);

    std::string get_SelectedItem();

    ::Event SelectionChanged;

    ::int2 get_Size();

    void set_Size(::int2 value);

    int32_t TabIndex;

    int32_t get_TabIndex();
    void set_TabIndex(int32_t value);

    void ActivateFromKey(::Keys key);

    bool CanActivateWithKey(::Keys key);

    ComboBoxComponent(::int2 size, ::FontAsset* font, List<std::string>* items, int32_t selectedIndex);

    void ComponentAdded(::Entity* entity);

    void ComponentRemoved(::Entity* entity);

    bool ContainsScreenPoint(::int2 point);

    void ParentEnabledChange(bool newEnabled);

    void SetItems(List<std::string>* items, int32_t selectedIndex);

    void SetRenderOrders(uint8_t backgroundOrder, uint8_t textOrder, uint8_t listBackgroundOrder, uint8_t listTextOrder);

    void SetTargetFocused(bool isFocused);

    void Update();

    ::Entity* get_Parent();

    void set_Parent(::Entity* value);
private:
    static std::string ArrowGlyph;

    static int32_t ArrowPaddingX;

    static int32_t ListGap;

    static int32_t TextPaddingX;

    ::Entity* arrowEntity;

    ::TextComponent* arrowText;

    ::RoundedRectComponent* background;

    uint8_t backgroundOrder;

    ::FontAsset* font;

    bool hasRenderOrderOverrides;

    ::InteractableComponent* interactable;

    bool isHovering;

    bool isOpen;

    bool isPressed;

    int32_t itemHeight;

    List<::ComboBoxItemVisual*>* itemVisuals;

    List<std::string>* items;

    ::Entity* labelEntity;

    ::TextComponent* labelText;

    ::RoundedRectComponent* listBackground;

    uint8_t listBackgroundOrder;

    ::Entity* listRoot;

    uint8_t listTextOrder;

    int32_t selectedIndex;

    ::int2 size;

    uint8_t textOrder;

    void ApplyRenderOrders();

    void CopyItems(List<std::string>* source);

    ::ComboBoxItemVisual* CreateItemVisual();

    void EnsureItemVisuals(int32_t count);

    ::ICamera* FindTopmostCameraAt(int32_t x, int32_t y, uint16_t layerMask);

    float GetCornerRadius(::int2 size);

    void HandleItemCursorEvent(::ComboBoxItemVisual* entry, ::int2 relPos, ::int2 delta, ::PointerInteraction state);

    void HandleMainCursorEvent(::int2 relPos, ::int2 delta, ::PointerInteraction state);

    void HideItemVisuals();

    bool IsPointerInsideCombo(::int2 mousePosition);

    void ResetItemStates();

    void SetSelectedIndexInternal(int32_t index, bool raiseEvent);

    void UpdateAllItemStates();

    void UpdateDropdownVisibility();

    void UpdateItemVisualState(::ComboBoxItemVisual* entry, bool isSelected);

    void UpdateLabelLayout();

    void UpdateLabelText();

    void UpdateLayout();

    void UpdateListLayout();

    void UpdateMainLayout();

    void UpdateMainVisual();

    void ValidateItems(List<std::string>* items);

    int32_t ValidateSelectedIndex(int32_t itemCount, int32_t index);
};
