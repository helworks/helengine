#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class FontChar;
class FontAsset;

#include "runtime/native_exceptions.hpp"
#include "runtime/native_string.hpp"
#include "system/text/string-builder.hpp"
#include "FontChar.hpp"
#include "FontAsset.hpp"

class TextLayoutUtils
{
public:
    virtual ~TextLayoutUtils() = default;

    static std::string WrapText(std::string text, ::FontAsset* font, int32_t maxWidth);
private:
    static double GetCharacterAdvanceWidth(::FontAsset* font, char character);
};
