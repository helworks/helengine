#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class int2;

#include "int2.hpp"

class IFocusGroup
{
public:
    virtual bool get_CanReceiveFocus() = 0;

    virtual int32_t get_GroupOrder() = 0;

    virtual ::IFocusGroup* get_RootGroup() = 0;

    virtual bool ContainsScreenPoint(::int2 point) = 0;

    virtual void SetGroupActive(bool isActive) = 0;
};
