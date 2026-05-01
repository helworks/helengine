#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class float2;
class FontTightMetrics;
class FontChar;
class FontInfo;
class RuntimeTexture;

#include "runtime/native_disposable.hpp"
#include "system/math.hpp"
#include "system/math.hpp"
#include "float2.hpp"
#include "FontTightMetrics.hpp"
#include "runtime/native_dictionary.hpp"
#include "FontChar.hpp"
#include "FontInfo.hpp"
#include "RuntimeTexture.hpp"
#include "float2.hpp"
#include "runtime/native_string.hpp"
#include "FontTightMetrics.hpp"

class FontAsset : public IDisposable
{
public:
    int32_t AtlasHeight;

    int32_t get_AtlasHeight();
    void set_AtlasHeight(int32_t value);

    int32_t AtlasWidth;

    int32_t get_AtlasWidth();
    void set_AtlasWidth(int32_t value);

    Dictionary<char, ::FontChar>* Characters;

    Dictionary<char, ::FontChar>* get_Characters();
    void set_Characters(Dictionary<char, ::FontChar>* value);

    ::FontInfo* FontInfo;

    ::FontInfo* get_FontInfo();
    void set_FontInfo(::FontInfo* value);

    float LineHeight;

    float get_LineHeight();
    void set_LineHeight(float value);

    ::RuntimeTexture* Texture;

    ::RuntimeTexture* get_Texture();
    void set_Texture(::RuntimeTexture* value);

    void Dispose();

    FontAsset(::FontInfo* fontInfo, ::RuntimeTexture* tex, Dictionary<char, ::FontChar>* chars, float lineHeight, int32_t atlasWidth, int32_t atlasHeight);

    ::float2 MeasureString(std::string text);

    ::FontTightMetrics MeasureTight(std::string text);
};
