#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

#include "runtime/native_string.hpp"

class TextContent
{
public:
    virtual ~TextContent() = default;

    TextContent();

    std::string Text;

    std::string get_Text();
    void set_Text(std::string value);
};
