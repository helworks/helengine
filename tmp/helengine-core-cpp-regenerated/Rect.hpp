#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class Rect
{
public:
    virtual ~Rect() = default;

    Rect();

    int32_t H;

    int32_t W;

    int32_t X;

    int32_t Y;

    Rect(int32_t x, int32_t y, int32_t w, int32_t h);
};
