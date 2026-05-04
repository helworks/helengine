#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class IFocusGroup;

#include "IFocusGroup.hpp"
#include "Keys.hpp"

class IFocusTarget
{
public:
    virtual bool get_CanReceiveFocus() = 0;

    virtual ::IFocusGroup* get_FocusGroup() = 0;

    virtual bool get_IsDefaultTarget() = 0;

    virtual int32_t get_TabIndex() = 0;

    virtual void ActivateFromKey(Keys key) = 0;

    virtual bool CanActivateWithKey(Keys key) = 0;

    virtual bool ContainsScreenPoint(int32_t x, int32_t y) = 0;

    virtual void SetTargetFocused(bool isFocused) = 0;
};
