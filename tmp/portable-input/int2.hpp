#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

#include "runtime/native_string.hpp"

class int2
{
public:
    virtual ~int2() = default;

    int2();

    static ::int2 Zero;

    int32_t X;

    int32_t Y;

    std::string ToString();

    int2(int32_t x, int32_t y);
};
