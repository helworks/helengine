#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class Rect;
class TextureAsset;
class float4;

#include "system/math.hpp"
#include "system/math.hpp"
#include "runtime/array.hpp"
#include "runtime/array.hpp"
#include "Rect.hpp"
#include "TextureAsset.hpp"
#include "TextureAsset.hpp"
#include "runtime/array.hpp"
#include "float4.hpp"
#include "runtime/array.hpp"
#include "runtime/array.hpp"

class NineSliceAtlas
{
public:
    Array<::float4>* BorderUV;

    Array<::float4>* get_BorderUV();
    void set_BorderUV(Array<::float4>* value);

    int32_t CornerSize;

    int32_t get_CornerSize();
    void set_CornerSize(int32_t value);

    int32_t EdgeThickness;

    int32_t get_EdgeThickness();
    void set_EdgeThickness(int32_t value);

    Array<::float4>* FillUV;

    Array<::float4>* get_FillUV();
    void set_FillUV(Array<::float4>* value);

    int32_t Height;

    int32_t get_Height();
    void set_Height(int32_t value);

    int32_t Padding;

    int32_t get_Padding();
    void set_Padding(int32_t value);

    ::TextureAsset* Texture;

    ::TextureAsset* get_Texture();
    void set_Texture(::TextureAsset* value);

    int32_t Width;

    int32_t get_Width();
    void set_Width(int32_t value);

    static ::NineSliceAtlas* Generate(int32_t radiusPx, int32_t borderPx, int32_t aaPx, int32_t padding);
private:
    static void Blit(Array<float>* src, int32_t srcW, int32_t srcH, ::Rect s, Array<uint8_t>* dst, int32_t dstW, int32_t dstH, ::Rect d);

    NineSliceAtlas();

    static Array<float>* RasterizeRoundedRectAlpha(int32_t w, int32_t h, int32_t radius, int32_t aa);

    static Array<float>* RasterizeRoundedRectBorderAlpha(int32_t w, int32_t h, int32_t radius, int32_t borderPx, int32_t aa);

    static float SmoothStep(float edge0, float edge1, float x);
};
