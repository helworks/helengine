#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

#include "runtime/native_string.hpp"

class byte4
{
public:
    byte4();

    uint8_t W;

    uint8_t X;

    uint8_t Y;

    uint8_t Z;

    std::string ToString();

    byte4(uint8_t x, uint8_t y, uint8_t z, uint8_t w);
};
