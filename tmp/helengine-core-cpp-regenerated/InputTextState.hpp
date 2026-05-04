#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

#include "runtime/array.hpp"

class InputTextState
{
public:
    virtual ~InputTextState() = default;

    InputTextState();

    int32_t CharacterCount;

    int32_t get_CharacterCount();
    void set_CharacterCount(int32_t value);

    Array<char>* Characters;

    Array<char>* get_Characters();
    void set_Characters(Array<char>* value);
};
