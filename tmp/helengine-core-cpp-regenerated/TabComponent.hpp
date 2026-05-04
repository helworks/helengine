#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class ButtonComponent;
class IFocusTarget;
class IAnchorSizeProvider;
class FontAsset;

#include "ButtonComponent.hpp"
#include "IFocusTarget.hpp"
#include "IAnchorSizeProvider.hpp"
#include "runtime/native_string.hpp"
#include "int2.hpp"
#include "FontAsset.hpp"
#include "system/action.hpp"

class TabComponent : public ButtonComponent
{
public:
    virtual ~TabComponent() = default;

    bool get_IsSelected();

    void SetSelected(bool isSelected);

    TabComponent(std::string text, int2* size, ::FontAsset* font, Action<>* onClickAction, float borderThickness);

    int2* get_AnchorSize();

    bool get_CanReceiveFocus();

    ::RoundedRectCorners get_Corners();

    void set_Corners(::RoundedRectCorners value);

    ::IFocusGroup* get_FocusGroup();

    void set_FocusGroup(::IFocusGroup* value);

    bool get_IsDefaultTarget();

    void set_IsDefaultTarget(bool value);

    bool get_IsKeyboardFocused();

    void set_IsKeyboardFocused(bool value);

    int2* get_Size();

    int32_t get_TabIndex();

    void set_TabIndex(int32_t value);

    ::Entity* get_Parent();

    void set_Parent(::Entity* value);
};
