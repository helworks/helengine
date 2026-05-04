#ifdef DrawText
#undef DrawText
#endif
#include "KeyboardState.hpp"
#include "runtime/array.hpp"
#include "runtime/native_exceptions.hpp"
#include "Keys.hpp"
#include "runtime/array.hpp"
#include "runtime/finally.hpp"
#include "runtime/native_dictionary.hpp"
#include "runtime/native_enum.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_string.hpp"
#include "system/math.hpp"

KeyboardState::KeyboardState() : _keys0(0), _keys1(0), _keys2(0), _keys3(0), _keys4(0), _keys5(0), _keys6(0), _keys7(0), _modifiers()
{
}

bool KeyboardState::get_CapsLock()
{
return (this->_modifiers & CapsLockModifier) > 0;}

bool KeyboardState::get_NumLock()
{
return (this->_modifiers & NumLockModifier) > 0;}

bool KeyboardState::Equals(void* obj)
{
    if (obj != nullptr)
    {
return (*this) == (*static_cast<KeyboardState*>(obj));    }
return false;}

int32_t KeyboardState::GetHashCode()
{
return static_cast<int32_t>((this->_keys0 ^ this->_keys1 ^ this->_keys2 ^ this->_keys3 ^ this->_keys4 ^ this->_keys5 ^ this->_keys6 ^ this->_keys7));}

int32_t KeyboardState::GetPressedKeyCount()
{
const uint32_t count = this->CountBits(this->_keys0) + this->CountBits(this->_keys1) + this->CountBits(this->_keys2) + this->CountBits(this->_keys3) + this->CountBits(this->_keys4) + this->CountBits(this->_keys5) + this->CountBits(this->_keys6) + this->CountBits(this->_keys7);
return static_cast<int32_t>(count);}

void KeyboardState::GetPressedKeys(Array<::Keys>* keys)
{
    if (keys == nullptr)
    {
throw new ArgumentNullException("keys");
    }
const uint32_t count = this->CountBits(this->_keys0) + this->CountBits(this->_keys1) + this->CountBits(this->_keys2) + this->CountBits(this->_keys3) + this->CountBits(this->_keys4) + this->CountBits(this->_keys5) + this->CountBits(this->_keys6) + this->CountBits(this->_keys7);
    if (count > keys->Length)
    {
throw new ArgumentOutOfRangeException("keys", "The supplied array cannot fit the number of pressed keys. Call GetPressedKeyCount() to get the number of pressed keys.");
    }
int32_t index = 0;
    if (this->_keys0 != 0 && index < keys->Length)
    {
index = this->AddKeysToArray(this->_keys0, 0 * 32, keys, index);
    }
    if (this->_keys1 != 0 && index < keys->Length)
    {
index = this->AddKeysToArray(this->_keys1, 1 * 32, keys, index);
    }
    if (this->_keys2 != 0 && index < keys->Length)
    {
index = this->AddKeysToArray(this->_keys2, 2 * 32, keys, index);
    }
    if (this->_keys3 != 0 && index < keys->Length)
    {
index = this->AddKeysToArray(this->_keys3, 3 * 32, keys, index);
    }
    if (this->_keys4 != 0 && index < keys->Length)
    {
index = this->AddKeysToArray(this->_keys4, 4 * 32, keys, index);
    }
    if (this->_keys5 != 0 && index < keys->Length)
    {
index = this->AddKeysToArray(this->_keys5, 5 * 32, keys, index);
    }
    if (this->_keys6 != 0 && index < keys->Length)
    {
index = this->AddKeysToArray(this->_keys6, 6 * 32, keys, index);
    }
    if (this->_keys7 != 0 && index < keys->Length)
    {
index = this->AddKeysToArray(this->_keys7, 7 * 32, keys, index);
    }
}

Array<::Keys>* KeyboardState::GetPressedKeys()
{
const uint32_t count = this->CountBits(this->_keys0) + this->CountBits(this->_keys1) + this->CountBits(this->_keys2) + this->CountBits(this->_keys3) + this->CountBits(this->_keys4) + this->CountBits(this->_keys5) + this->CountBits(this->_keys6) + this->CountBits(this->_keys7);
    if (count == 0)
    {
return empty;    }
Array<::Keys> *keys = new Array<Keys>(count);
int32_t index = 0;
    if (this->_keys0 != 0)
    {
index = this->AddKeysToArray(this->_keys0, 0 * 32, keys, index);
    }
    if (this->_keys1 != 0)
    {
index = this->AddKeysToArray(this->_keys1, 1 * 32, keys, index);
    }
    if (this->_keys2 != 0)
    {
index = this->AddKeysToArray(this->_keys2, 2 * 32, keys, index);
    }
    if (this->_keys3 != 0)
    {
index = this->AddKeysToArray(this->_keys3, 3 * 32, keys, index);
    }
    if (this->_keys4 != 0)
    {
index = this->AddKeysToArray(this->_keys4, 4 * 32, keys, index);
    }
    if (this->_keys5 != 0)
    {
index = this->AddKeysToArray(this->_keys5, 5 * 32, keys, index);
    }
    if (this->_keys6 != 0)
    {
index = this->AddKeysToArray(this->_keys6, 6 * 32, keys, index);
    }
    if (this->_keys7 != 0)
    {
index = this->AddKeysToArray(this->_keys7, 7 * 32, keys, index);
    }
return keys;}

void KeyboardState::InternalClearAllKeys()
{
this->_keys0 = 0;
this->_keys1 = 0;
this->_keys2 = 0;
this->_keys3 = 0;
this->_keys4 = 0;
this->_keys5 = 0;
this->_keys6 = 0;
this->_keys7 = 0;
}

void KeyboardState::InternalClearKey(::Keys key)
{
const uint32_t mask = static_cast<uint32_t>(1) << ((static_cast<int32_t>(key)) & 0x1f);
switch ((static_cast<int32_t>(key)) >> 5) {
case 0: {
this->_keys0 &= ~mask;
break;
}
case 1: {
this->_keys1 &= ~mask;
break;
}
case 2: {
this->_keys2 &= ~mask;
break;
}
case 3: {
this->_keys3 &= ~mask;
break;
}
case 4: {
this->_keys4 &= ~mask;
break;
}
case 5: {
this->_keys5 &= ~mask;
break;
}
case 6: {
this->_keys6 &= ~mask;
break;
}
case 7: {
this->_keys7 &= ~mask;
break;
}
}

}

void KeyboardState::InternalSetKey(::Keys key)
{
const uint32_t mask = static_cast<uint32_t>(1) << ((static_cast<int32_t>(key)) & 0x1f);
switch ((static_cast<int32_t>(key)) >> 5) {
case 0: {
this->_keys0 |= mask;
break;
}
case 1: {
this->_keys1 |= mask;
break;
}
case 2: {
this->_keys2 |= mask;
break;
}
case 3: {
this->_keys3 |= mask;
break;
}
case 4: {
this->_keys4 |= mask;
break;
}
case 5: {
this->_keys5 |= mask;
break;
}
case 6: {
this->_keys6 |= mask;
break;
}
case 7: {
this->_keys7 |= mask;
break;
}
}

}

bool KeyboardState::IsKeyDown(::Keys key)
{
return this->InternalGetKey(key);}

bool KeyboardState::IsKeyUp(::Keys key)
{
return !this->InternalGetKey(key);}

KeyboardState::KeyboardState(List<::Keys>* keys, bool capsLock, bool numLock) : KeyboardState()
{
this->_modifiers = static_cast<uint8_t>((0 | (capsLock ? CapsLockModifier : 0) | (numLock ? NumLockModifier : 0)));
    if (keys != nullptr)
    {
for (const auto& k : *keys) {
InternalSetKey(k);
}
    }
}

KeyboardState::KeyboardState(Array<::Keys>* keys, bool capsLock, bool numLock) : KeyboardState()
{
this->_modifiers = static_cast<uint8_t>((0 | (capsLock ? CapsLockModifier : 0) | (numLock ? NumLockModifier : 0)));
    if (keys != nullptr)
    {
for (const auto& k : keys) {
InternalSetKey(k);
}
    }
}

KeyboardState::KeyboardState(Array<::Keys>* keys) : KeyboardState()
{
    if (keys != nullptr)
    {
for (const auto& k : keys) {
InternalSetKey(k);
}
    }
}

uint8_t KeyboardState::CapsLockModifier = 1;

uint8_t KeyboardState::NumLockModifier = 2;

Array<::Keys>* KeyboardState::empty = new Array<Keys>(0);

int32_t KeyboardState::AddKeysToArray(uint32_t keys, int32_t offset, Array<::Keys>* pressedKeys, int32_t index)
{
for (int32_t i = 0; i < 32; i++) {
    if ((keys & (1 << i)) != 0)
    {
(*pressedKeys)[index++] = static_cast<Keys>((offset + i));
    }
}
return index;}

uint32_t KeyboardState::CountBits(uint32_t v)
{
v = v - ((v >> 1) & 0x55555555);
v = (v & 0x33333333) + ((v >> 2) & 0x33333333);
return ((v + (v >> 4) & 0xF0F0F0F) * 0x1010101) >> 24;}

bool KeyboardState::InternalGetKey(::Keys key)
{
const uint32_t mask = static_cast<uint32_t>(1) << ((static_cast<int32_t>(key)) & 0x1f);
uint32_t element;
switch ((static_cast<int32_t>(key)) >> 5) {
case 0: {
element = this->_keys0;
break;
}
case 1: {
element = this->_keys1;
break;
}
case 2: {
element = this->_keys2;
break;
}
case 3: {
element = this->_keys3;
break;
}
case 4: {
element = this->_keys4;
break;
}
case 5: {
element = this->_keys5;
break;
}
case 6: {
element = this->_keys6;
break;
}
case 7: {
element = this->_keys7;
break;
}
default:  {
element = 0;
break;
}
}

return (element & mask) != 0;}

bool operator!=(::KeyboardState a, ::KeyboardState b)
{
return !(a == b);}

bool operator==(::KeyboardState a, ::KeyboardState b)
{
return a._keys0 == b._keys0 && a._keys1 == b._keys1 && a._keys2 == b._keys2 && a._keys3 == b._keys3 && a._keys4 == b._keys4 && a._keys5 == b._keys5 && a._keys6 == b._keys6 && a._keys7 == b._keys7;}

