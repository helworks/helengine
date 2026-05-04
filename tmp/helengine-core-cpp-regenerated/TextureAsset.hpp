#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class Asset;

#include "Asset.hpp"
#include "runtime/array.hpp"

class TextureAsset : public Asset
{
public:
    virtual ~TextureAsset() = default;

    TextureAsset();

    Array<uint8_t>* Colors;

    uint16_t Height;

    uint16_t Width;

    std::string get_Id();

    void set_Id(std::string value);
};
