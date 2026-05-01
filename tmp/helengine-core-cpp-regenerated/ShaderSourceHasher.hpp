#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

#include "runtime/native_exceptions.hpp"
#include "runtime/array.hpp"
#include "system/text/encoding.hpp"
#include "system/text/encoding.hpp"
#include "SHA256.hpp"
#include "SHA256.hpp"
#include "runtime/native_string.hpp"
#include "runtime/array.hpp"
#include "runtime/array.hpp"

class ShaderSourceHasher
{
public:
    std::string ComputeHash(std::string source);
private:
    std::string ConvertToHex(Array<uint8_t>* data);

    char ToHexChar(int32_t value);
};
