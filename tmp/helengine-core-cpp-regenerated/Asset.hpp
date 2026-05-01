#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

#include "runtime/native_string.hpp"

class Asset
{
public:
    Asset();

    std::string Id;

    std::string get_Id();
    void set_Id(std::string value);
};
