#ifdef DrawText
#undef DrawText
#endif
#include "NineSliceAtlas.hpp"
#include "system/math.hpp"
#include "runtime/array.hpp"
#include "Rect.hpp"
#include "TextureAsset.hpp"
#include "NineSliceAtlas.hpp"
#include "float4.hpp"
#include "runtime/array.hpp"
#include "runtime/native_cast.hpp"
#include "runtime/native_datetime.hpp"
#include "runtime/native_dictionary.hpp"
#include "runtime/native_disposable.hpp"
#include "runtime/native_enum.hpp"
#include "runtime/native_event.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_nullable.hpp"
#include "runtime/native_span.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_tuple.hpp"
#include "runtime/native_type.hpp"
#include "system/app_context.hpp"
#include "system/bit_converter.hpp"
#include "system/diagnostics/debug.hpp"
#include "system/guid.hpp"
#include "system/io/file-stream.hpp"
#include "system/io/file.hpp"
#include "system/io/memory-stream.hpp"
#include "system/io/path.hpp"
#include "system/io/stream.hpp"
#include "system/math.hpp"
#include "system/number.hpp"
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"
#include "system/text/regular_expressions/regex.hpp"

Array<::float4>* NineSliceAtlas::get_BorderUV()
{
return this->BorderUV;
}

void NineSliceAtlas::set_BorderUV(Array<::float4>* value)
{
this->BorderUV = value;
}

int32_t NineSliceAtlas::get_CornerSize()
{
return this->CornerSize;
}

void NineSliceAtlas::set_CornerSize(int32_t value)
{
this->CornerSize = value;
}

int32_t NineSliceAtlas::get_EdgeThickness()
{
return this->EdgeThickness;
}

void NineSliceAtlas::set_EdgeThickness(int32_t value)
{
this->EdgeThickness = value;
}

Array<::float4>* NineSliceAtlas::get_FillUV()
{
return this->FillUV;
}

void NineSliceAtlas::set_FillUV(Array<::float4>* value)
{
this->FillUV = value;
}

int32_t NineSliceAtlas::get_Height()
{
return this->Height;
}

void NineSliceAtlas::set_Height(int32_t value)
{
this->Height = value;
}

int32_t NineSliceAtlas::get_Padding()
{
return this->Padding;
}

void NineSliceAtlas::set_Padding(int32_t value)
{
this->Padding = value;
}

::TextureAsset* NineSliceAtlas::get_Texture()
{
return this->Texture;
}

void NineSliceAtlas::set_Texture(::TextureAsset* value)
{
this->Texture = value;
}

int32_t NineSliceAtlas::get_Width()
{
return this->Width;
}

void NineSliceAtlas::set_Width(int32_t value)
{
this->Width = value;
}

::NineSliceAtlas* NineSliceAtlas::Generate(int32_t radiusPx, int32_t borderPx, int32_t aaPx, int32_t padding)
{
const int32_t s = Math::Max(1, radiusPx);
const int32_t e = 1;
const int32_t pad = Math::Max(1, padding);
const int32_t refW = 2 * s + e;
const int32_t refH = 2 * s + e;
const int32_t tileW0 = s;
const int32_t tileW1 = e;
const int32_t tileH0 = s;
const int32_t tileH1 = e;
const int32_t atlasW = pad + tileW0 + pad + tileW1 + pad + tileW0 + pad;
const int32_t atlasH = pad + tileH0 + pad + tileH1 + pad + tileH0 + pad + pad + tileH0 + pad + tileH1 + pad + tileH0 + pad;
Array<uint8_t> *rgba = new Array<uint8_t>(atlasW * atlasH * 4);
Array<float> *refFill = RasterizeRoundedRectAlpha(refW, refH, s, aaPx);
Array<float> *refBorder = borderPx > 0 ? RasterizeRoundedRectBorderAlpha(refW, refH, s, borderPx, aaPx) : new Array<float>(refW * refH);
::Rect srcTopLeft = ::Rect(0, 0, s, s);
::Rect srcTop = ::Rect(s, 0, e, s);
::Rect srcTopRight = ::Rect(s + e, 0, s, s);
::Rect srcMidLeft = ::Rect(0, s, s, e);
::Rect srcMid = ::Rect(s, s, e, e);
::Rect srcMidRight = ::Rect(s + e, s, s, e);
::Rect srcBotLeft = ::Rect(0, s + e, s, s);
::Rect srcBot = ::Rect(s, s + e, e, s);
::Rect srcBotRight = ::Rect(s + e, s + e, s, s);
auto Dst = [&](int32_t row, int32_t col, int32_t w, int32_t h) -> Rect {
int32_t y = pad;
for (int32_t r = 0; r < row; r++) {
const int32_t rh = (r % 3 == 0 || r % 3 == 2) ? tileH0 : tileH1;
y += rh + pad;
}
int32_t x = pad;
for (int32_t c = 0; c < col; c++) {
const int32_t cw = (c == 1) ? tileW1 : tileW0;
x += cw + pad;
}
return ::Rect(x, y, w, h);};
Blit(refFill, refW, refH, srcTopLeft, rgba, atlasW, atlasH, Dst(0, 0, tileW0, tileH0));
Blit(refFill, refW, refH, srcTop, rgba, atlasW, atlasH, Dst(0, 1, tileW1, tileH0));
Blit(refFill, refW, refH, srcTopRight, rgba, atlasW, atlasH, Dst(0, 2, tileW0, tileH0));
Blit(refFill, refW, refH, srcMidLeft, rgba, atlasW, atlasH, Dst(1, 0, tileW0, tileH1));
Blit(refFill, refW, refH, srcMid, rgba, atlasW, atlasH, Dst(1, 1, tileW1, tileH1));
Blit(refFill, refW, refH, srcMidRight, rgba, atlasW, atlasH, Dst(1, 2, tileW0, tileH1));
Blit(refFill, refW, refH, srcBotLeft, rgba, atlasW, atlasH, Dst(2, 0, tileW0, tileH0));
Blit(refFill, refW, refH, srcBot, rgba, atlasW, atlasH, Dst(2, 1, tileW1, tileH0));
Blit(refFill, refW, refH, srcBotRight, rgba, atlasW, atlasH, Dst(2, 2, tileW0, tileH0));
    if (borderPx > 0)
    {
Blit(refBorder, refW, refH, srcTopLeft, rgba, atlasW, atlasH, Dst(3, 0, tileW0, tileH0));
Blit(refBorder, refW, refH, srcTop, rgba, atlasW, atlasH, Dst(3, 1, tileW1, tileH0));
Blit(refBorder, refW, refH, srcTopRight, rgba, atlasW, atlasH, Dst(3, 2, tileW0, tileH0));
Blit(refBorder, refW, refH, srcMidLeft, rgba, atlasW, atlasH, Dst(4, 0, tileW0, tileH1));
Blit(refBorder, refW, refH, srcMid, rgba, atlasW, atlasH, Dst(4, 1, tileW1, tileH1));
Blit(refBorder, refW, refH, srcMidRight, rgba, atlasW, atlasH, Dst(4, 2, tileW0, tileH1));
Blit(refBorder, refW, refH, srcBotLeft, rgba, atlasW, atlasH, Dst(5, 0, tileW0, tileH0));
Blit(refBorder, refW, refH, srcBot, rgba, atlasW, atlasH, Dst(5, 1, tileW1, tileH0));
Blit(refBorder, refW, refH, srcBotRight, rgba, atlasW, atlasH, Dst(5, 2, tileW0, tileH0));
    }
::TextureAsset *tex = ([&]() {
auto __object_000000B4 = new ::TextureAsset();
__object_000000B4->Colors = rgba;
__object_000000B4->Width = static_cast<uint16_t>(atlasW);
__object_000000B4->Height = static_cast<uint16_t>(atlasH);
return __object_000000B4;
})();
::NineSliceAtlas *atlas = new ::NineSliceAtlas();
atlas->set_Texture(tex);
atlas->set_CornerSize(s);
atlas->set_EdgeThickness(e);
atlas->set_Padding(pad);
atlas->set_Width(atlasW);
atlas->set_Height(atlasH);
const float AW = atlasW;
const float AH = atlasH;
auto x0 = [&](int32_t col) -> float { return (col == 0 ? pad : (col == 1 ? pad + tileW0 + pad : pad + tileW0 + pad + tileW1 + pad)); };
auto y0 = [&](int32_t row) -> float {
int32_t y = pad;
for (int32_t r = 0; r < row; r++) {
const int32_t rh = (r % 3 == 0 || r % 3 == 2) ? tileH0 : tileH1;
y += rh + pad;
}
return y;};
auto tw = [&](int32_t col) -> int32_t { return (col == 1 ? tileW1 : tileW0); };
auto th = [&](int32_t row) -> int32_t { return ((row % 3 == 0 || row % 3 == 2) ? tileH0 : tileH1); };
for (int32_t r = 0; r < 3; r++) {
for (int32_t c = 0; c < 3; c++) {
const float ux = x0(c) / AW;
const float uy = y0(r) / AH;
const float uw = tw(c) / AW;
const float uh = th(r) / AH;
(*atlas->get_FillUV())[r * 3 + c] = ::float4(ux, uy, uw, uh);
}
}
for (int32_t r = 0; r < 3; r++) {
for (int32_t c = 0; c < 3; c++) {
const float ux = x0(c) / AW;
const float uy = y0(r + 3) / AH;
const float uw = tw(c) / AW;
const float uh = th(r) / AH;
(*atlas->get_BorderUV())[r * 3 + c] = ::float4(ux, uy, uw, uh);
}
}
return atlas;}

void NineSliceAtlas::Blit(Array<float>* src, int32_t srcW, int32_t srcH, ::Rect s, Array<uint8_t>* dst, int32_t dstW, int32_t dstH, ::Rect d)
{
for (int32_t y = 0; y < s.H; y++) {
for (int32_t x = 0; x < s.W; x++) {
const int32_t sx = s.X + x;
const int32_t sy = s.Y + y;
const float a = (*src)[sy * srcW + sx];
    if (a <= 0)
    {
continue;
    }
const int32_t dx = d.X + x;
const int32_t dy = d.Y + y;
const int32_t di = (dy * dstW + dx) * 4;
const uint8_t A = static_cast<uint8_t>(Math::Clamp(static_cast<int32_t>((a * 255)), 0, 255));
(*dst)[di + 0] = 255;
(*dst)[di + 1] = 255;
(*dst)[di + 2] = 255;
(*dst)[di + 3] = A;
}
}
}

NineSliceAtlas::NineSliceAtlas() : BorderUV(new Array<float4>(9)), CornerSize(0), EdgeThickness(0), FillUV(new Array<float4>(9)), Height(0), Padding(0), Texture(), Width(0)
{
}

Array<float>* NineSliceAtlas::RasterizeRoundedRectAlpha(int32_t w, int32_t h, int32_t radius, int32_t aa)
{
Array<float> *a = new Array<float>(w * h);
const float cx = w * 0.5f;
const float cy = h * 0.5f;
const float halfX = w * 0.5f;
const float halfY = h * 0.5f;
const float r = radius;
for (int32_t y = 0; y < h; y++) {
for (int32_t x = 0; x < w; x++) {
const float px = (x + 0.5f - cx);
const float py = (y + 0.5f - cy);
const float qx = MathF::Abs(px) - (halfX - r);
const float qy = MathF::Abs(py) - (halfY - r);
const float qxm = MathF::Max(qx, 0);
const float qym = MathF::Max(qy, 0);
const float dist = MathF::Sqrt(qxm * qxm + qym * qym) + MathF::Min(MathF::Max(qx, qy), 0.0f) - r;
float alpha = 1.0f - SmoothStep(-aa, aa, dist);
    if (alpha < 0)
    {
alpha = 0;
    }
    if (alpha > 1)
    {
alpha = 1;
    }
(*a)[y * w + x] = alpha;
}
}
return a;}

Array<float>* NineSliceAtlas::RasterizeRoundedRectBorderAlpha(int32_t w, int32_t h, int32_t radius, int32_t borderPx, int32_t aa)
{
Array<float> *outer = RasterizeRoundedRectAlpha(w, h, radius, aa);
const int32_t ir = Math::Max(0, radius - borderPx);
Array<float> *inner = RasterizeRoundedRectAlpha(w, h, ir, aa);
Array<float> *a = new Array<float>(w * h);
for (int32_t i = 0; i < a->Length; i++) {
float v = (*outer)[i] - (*inner)[i];
    if (v < 0)
    {
v = 0;
    }
    if (v > 1)
    {
v = 1;
    }
(*a)[i] = v;
}
return a;}

float NineSliceAtlas::SmoothStep(float edge0, float edge1, float x)
{
const float t = Math::Clamp((x - edge0) / (edge1 - edge0), 0.0f, 1.0f);
return t * t * (3.0f - 2.0f * t);}

