#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class Asset;

#include "Asset.hpp"
#include "runtime/native_string.hpp"

class TextAsset : public Asset
{
public:
    virtual ~TextAsset() = default;

    TextAsset();

    std::string Text;

    std::string get_Id();

    void set_Id(std::string value);
};
