#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

#include "runtime/array.hpp"
#include "runtime/array.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/array.hpp"
#include "Keys.hpp"
#include "runtime/native_list.hpp"

class KeyboardState
{
public:
    virtual ~KeyboardState() = default;

    KeyboardState();

    bool get_CapsLock();

    bool get_NumLock();

    bool Equals(void* obj);

    int32_t GetHashCode();

    int32_t GetPressedKeyCount();

    void GetPressedKeys(Array<::Keys>* keys);

    Array<::Keys>* GetPressedKeys();

    void InternalClearAllKeys();

    void InternalClearKey(::Keys key);

    void InternalSetKey(::Keys key);

    bool IsKeyDown(::Keys key);

    bool IsKeyUp(::Keys key);

    KeyboardState(List<::Keys>* keys, bool capsLock, bool numLock);

    KeyboardState(Array<::Keys>* keys, bool capsLock, bool numLock);

    KeyboardState(Array<::Keys>* keys);

    friend bool operator!=(::KeyboardState a, ::KeyboardState b);

    friend bool operator==(::KeyboardState a, ::KeyboardState b);
private:
    static uint8_t CapsLockModifier;

    static uint8_t NumLockModifier;

    static Array<::Keys>* empty;

    uint32_t _keys0;

    uint32_t _keys1;

    uint32_t _keys2;

    uint32_t _keys3;

    uint32_t _keys4;

    uint32_t _keys5;

    uint32_t _keys6;

    uint32_t _keys7;

    uint8_t _modifiers;

    static int32_t AddKeysToArray(uint32_t keys, int32_t offset, Array<::Keys>* pressedKeys, int32_t index);

    static uint32_t CountBits(uint32_t v);

    bool InternalGetKey(::Keys key);
};
