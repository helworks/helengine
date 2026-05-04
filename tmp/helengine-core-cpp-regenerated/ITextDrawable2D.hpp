#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class IDrawable2D;
class byte4;
class FontAsset;
class float4;

#include "IDrawable2D.hpp"
#include "byte4.hpp"
#include "FontAsset.hpp"
#include "int2.hpp"
#include "float4.hpp"
#include "runtime/native_string.hpp"

class ITextDrawable2D : public IDrawable2D
{
public:
    virtual ::byte4 get_Color() = 0;

    virtual void set_Color(::byte4 value) = 0;

    virtual ::FontAsset* get_Font() = 0;

    virtual void set_Font(::FontAsset* value) = 0;

    virtual int2* get_Size() = 0;

    virtual void set_Size(int2* value) = 0;

    virtual ::float4 get_SourceRect() = 0;

    virtual void set_SourceRect(::float4 value) = 0;

    virtual std::string get_Text() = 0;

    virtual void set_Text(std::string value) = 0;

    virtual bool get_WrapText() = 0;

    virtual void set_WrapText(bool value) = 0;
};
