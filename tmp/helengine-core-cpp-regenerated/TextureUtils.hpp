#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class RuntimeTexture;

#include "RuntimeTexture.hpp"

class TextureUtils
{
public:
    virtual ~TextureUtils() = default;

    static ::RuntimeTexture* get_PixelTexture();
private:
    static ::RuntimeTexture* pixelTexture;
};
