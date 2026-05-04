#ifdef DrawText
#undef DrawText
#endif
#include "FontAsset.hpp"
#include "system/math.hpp"
#include "float2.hpp"
#include "FontTightMetrics.hpp"
#include "FontChar.hpp"
#include "runtime/array.hpp"
#include "runtime/finally.hpp"
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
#include "runtime/native_stack.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_tuple.hpp"
#include "runtime/native_type.hpp"
#include "system/app_context.hpp"
#include "system/binary_primitives.hpp"
#include "system/bit_converter.hpp"
#include "system/diagnostics/debug.hpp"
#include "system/diagnostics/stopwatch.hpp"
#include "system/guid.hpp"
#include "system/io/file-stream.hpp"
#include "system/io/file.hpp"
#include "system/io/memory-stream.hpp"
#include "system/io/path.hpp"
#include "system/io/stream-reader.hpp"
#include "system/io/stream.hpp"
#include "system/io/string-reader.hpp"
#include "system/math.hpp"
#include "system/number.hpp"
#include "system/security/cryptography/sha256.hpp"
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"
#include "system/text/regular_expressions/regex.hpp"
#include "system/text/string-builder.hpp"

int32_t FontAsset::get_AtlasHeight()
{
return this->AtlasHeight;
}

void FontAsset::set_AtlasHeight(int32_t value)
{
this->AtlasHeight = value;
}

int32_t FontAsset::get_AtlasWidth()
{
return this->AtlasWidth;
}

void FontAsset::set_AtlasWidth(int32_t value)
{
this->AtlasWidth = value;
}

Dictionary<char, ::FontChar>* FontAsset::get_Characters()
{
return this->Characters;
}

void FontAsset::set_Characters(Dictionary<char, ::FontChar>* value)
{
this->Characters = value;
}

::FontInfo* FontAsset::get_FontInfo()
{
return this->FontInfo;
}

void FontAsset::set_FontInfo(::FontInfo* value)
{
this->FontInfo = value;
}

float FontAsset::get_LineHeight()
{
return this->LineHeight;
}

void FontAsset::set_LineHeight(float value)
{
this->LineHeight = value;
}

::TextureAsset* FontAsset::get_SourceTextureAsset()
{
return this->SourceTextureAsset;
}

void FontAsset::set_SourceTextureAsset(::TextureAsset* value)
{
this->SourceTextureAsset = value;
}

::RuntimeTexture* FontAsset::get_Texture()
{
return this->Texture;
}

void FontAsset::set_Texture(::RuntimeTexture* value)
{
this->Texture = value;
}

void FontAsset::Dispose()
{
}

FontAsset::FontAsset(::FontInfo* fontInfo, ::RuntimeTexture* tex, Dictionary<char, ::FontChar>* chars, float lineHeight, int32_t atlasWidth, int32_t atlasHeight) : AtlasHeight(0), AtlasWidth(0), Characters(), FontInfo(), LineHeight(), SourceTextureAsset(), Texture()
{
this->set_LineHeight(lineHeight);
this->set_FontInfo(fontInfo);
this->set_Texture(tex);
this->set_Characters(chars);
this->set_AtlasWidth(atlasWidth);
this->set_AtlasHeight(atlasHeight);
}

::float2 FontAsset::MeasureString(std::string text)
{
float x = 0.0f;
float y = 0.0f;
float maxX = 0.0f;
const float line = Math::Max(this->LineHeight, 1.0f);
for (int32_t i = 0; i < static_cast<int32_t>(text.size()); i++) {
const char c = text[i];
    if (c == '\n')
    {
    if (x > maxX)
    {
maxX = x;
    }
y += line;
x = 0.0f;
continue;
    }
    if (c == ' ')
    {
x += this->FontInfo->get_SpaceWidth();
continue;
    }
::FontChar ch;
    if (this->Characters->TryGetValue(c, ch))
    {
const float adv = ch.AdvanceWidth > 0 ? ch.AdvanceWidth : ch.SourceRect.Z;
x += adv;
    }
}
    if (x > maxX)
    {
maxX = x;
    }
return ::float2(maxX, y + line);}

::FontTightMetrics FontAsset::MeasureTight(std::string text)
{
float width = 0.0f;
float minTop = 3.4028234663852886e38f;
float maxBottom = -3.4028234663852886e38f;
for (int32_t i = 0; i < static_cast<int32_t>(text.size()); i++) {
const char c = text[i];
    if (c == ' ')
    {
width += this->FontInfo->get_SpaceWidth();
continue;
    }
::FontChar ch;
    if (!this->Characters->TryGetValue(c, ch))
    {
continue;
    }
const float advance = ch.AdvanceWidth > 0 ? ch.AdvanceWidth : (ch.SourceRect.Z * AtlasWidth);
width += advance;
const float glyphTop = ch.OffsetY;
const float glyphBottom = ch.OffsetY + (ch.SourceRect.W * this->AtlasHeight);
    if (glyphTop < minTop)
    {
minTop = glyphTop;
    }
    if (glyphBottom > maxBottom)
    {
maxBottom = glyphBottom;
    }
}
    if (minTop == 3.4028234663852886e38f)
    {
minTop = 0.0f;
maxBottom = this->LineHeight > 0 ? this->LineHeight : 1.0f;
    }
return ::FontTightMetrics(width, minTop, maxBottom);}

