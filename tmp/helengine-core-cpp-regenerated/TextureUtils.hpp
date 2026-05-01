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
    static ::RuntimeTexture* get_PixelTexture();
private:
    static ::RuntimeTexture* pixelTexture;
};
