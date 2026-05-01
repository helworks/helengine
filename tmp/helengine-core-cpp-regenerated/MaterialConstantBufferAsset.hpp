#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

#include "runtime/array.hpp"
#include "runtime/native_string.hpp"

class MaterialConstantBufferAsset
{
public:
    Array<uint8_t>* Data;

    Array<uint8_t>* get_Data();
    void set_Data(Array<uint8_t>* value);

    std::string Name;

    std::string get_Name();
    void set_Name(std::string value);

    MaterialConstantBufferAsset();
};
