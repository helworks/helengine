#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class float4;

#include "float4.hpp"

class CameraClearSettings
{
public:
    CameraClearSettings();

    ::float4 ClearColor;

    ::float4 get_ClearColor();
    void set_ClearColor(::float4 value);

    bool ClearColorEnabled;

    bool get_ClearColorEnabled();
    void set_ClearColorEnabled(bool value);

    float ClearDepth;

    float get_ClearDepth();
    void set_ClearDepth(float value);

    bool ClearDepthEnabled;

    bool get_ClearDepthEnabled();
    void set_ClearDepthEnabled(bool value);

    uint8_t ClearStencil;

    uint8_t get_ClearStencil();
    void set_ClearStencil(uint8_t value);

    bool ClearStencilEnabled;

    bool get_ClearStencilEnabled();
    void set_ClearStencilEnabled(bool value);

    CameraClearSettings(bool clearColorEnabled, ::float4 clearColor, bool clearDepthEnabled, float clearDepth, bool clearStencilEnabled, uint8_t clearStencil);
};
