#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

#include "runtime/array.hpp"

class RawByteContent
{
public:
    RawByteContent();

    Array<uint8_t>* Bytes;

    Array<uint8_t>* get_Bytes();
    void set_Bytes(Array<uint8_t>* value);
};
