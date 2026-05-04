#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

#include "int2.hpp"

class IAnchorSizeProvider
{
public:
    virtual int2* get_AnchorSize() = 0;
};
