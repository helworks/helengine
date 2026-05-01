#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

#include "runtime/native_string.hpp"
#include "runtime/array.hpp"

class SceneComponentAssetRecord
{
public:
    SceneComponentAssetRecord();

    int32_t ComponentIndex;

    int32_t get_ComponentIndex();
    void set_ComponentIndex(int32_t value);

    std::string ComponentTypeId;

    std::string get_ComponentTypeId();
    void set_ComponentTypeId(std::string value);

    Array<uint8_t>* Payload;

    Array<uint8_t>* get_Payload();
    void set_Payload(Array<uint8_t>* value);
};
