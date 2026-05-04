#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class float4;

#include "float4.hpp"

class FontChar
{
public:
    virtual ~FontChar() = default;

    FontChar();

    float AdvanceWidth;

    float BearingX;

    float BearingY;

    float OffsetY;

    ::float4 SourceRect;

    FontChar(::float4 sourceRect, float offsetY, float advanceWidth, float bearingX, float bearingY);

    FontChar(::float4 r, float offsetY);
};
