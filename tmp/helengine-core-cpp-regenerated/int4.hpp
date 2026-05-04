#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

#include "runtime/native_string.hpp"

class int4
{
public:
    virtual ~int4() = default;

    int4();

    static ::int4 get_Identity();

    int32_t W;

    int32_t X;

    int32_t Y;

    int32_t Z;

    bool Contains(int32_t x, int32_t y);

    std::string ToString();

    int4(int32_t x, int32_t y, int32_t z, int32_t w);
private:
    static ::int4 identity;
};
