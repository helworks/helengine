#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

#include "runtime/native_string.hpp"
#include "runtime/native_exceptions.hpp"

class CoreInitializationOptions
{
public:
    virtual ~CoreInitializationOptions() = default;

    CoreInitializationOptions();

    std::string ContentRootPath;

    std::string get_ContentRootPath();
    void set_ContentRootPath(std::string value);

    int32_t RenderList2DInitialCapacity;

    int32_t get_RenderList2DInitialCapacity();
    void set_RenderList2DInitialCapacity(int32_t value);

    int32_t RenderList3DInitialCapacity;

    int32_t get_RenderList3DInitialCapacity();
    void set_RenderList3DInitialCapacity(int32_t value);

    uint8_t RenderOrderLayers3D;

    uint8_t get_RenderOrderLayers3D();
    void set_RenderOrderLayers3D(uint8_t value);

    int32_t UpdateListInitialCapacity;

    int32_t get_UpdateListInitialCapacity();
    void set_UpdateListInitialCapacity(int32_t value);

    uint8_t UpdateOrderLayers;

    uint8_t get_UpdateOrderLayers();
    void set_UpdateOrderLayers(uint8_t value);

    void Normalize();
};
