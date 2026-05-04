#ifdef DrawText
#undef DrawText
#endif
#include "CameraClearSettings.hpp"
#include "runtime/array.hpp"
#include "runtime/native_dictionary.hpp"
#include "runtime/native_disposable.hpp"
#include "runtime/native_enum.hpp"
#include "runtime/native_event.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_nullable.hpp"
#include "runtime/native_span.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_tuple.hpp"
#include "runtime/native_type.hpp"
#include "system/bit_converter.hpp"
#include "system/io/memory-stream.hpp"
#include "system/io/stream.hpp"
#include "system/text/encoding.hpp"

CameraClearSettings::CameraClearSettings() : ClearColor(), ClearColorEnabled(), ClearDepth(), ClearDepthEnabled(), ClearStencil(), ClearStencilEnabled()
{
}

::float4 CameraClearSettings::get_ClearColor()
{
return this->ClearColor;
}

void CameraClearSettings::set_ClearColor(::float4 value)
{
this->ClearColor = value;
}

bool CameraClearSettings::get_ClearColorEnabled()
{
return this->ClearColorEnabled;
}

void CameraClearSettings::set_ClearColorEnabled(bool value)
{
this->ClearColorEnabled = value;
}

float CameraClearSettings::get_ClearDepth()
{
return this->ClearDepth;
}

void CameraClearSettings::set_ClearDepth(float value)
{
this->ClearDepth = value;
}

bool CameraClearSettings::get_ClearDepthEnabled()
{
return this->ClearDepthEnabled;
}

void CameraClearSettings::set_ClearDepthEnabled(bool value)
{
this->ClearDepthEnabled = value;
}

uint8_t CameraClearSettings::get_ClearStencil()
{
return this->ClearStencil;
}

void CameraClearSettings::set_ClearStencil(uint8_t value)
{
this->ClearStencil = value;
}

bool CameraClearSettings::get_ClearStencilEnabled()
{
return this->ClearStencilEnabled;
}

void CameraClearSettings::set_ClearStencilEnabled(bool value)
{
this->ClearStencilEnabled = value;
}

CameraClearSettings::CameraClearSettings(bool clearColorEnabled, ::float4 clearColor, bool clearDepthEnabled, float clearDepth, bool clearStencilEnabled, uint8_t clearStencil) : ClearColor(), ClearColorEnabled(), ClearDepth(), ClearDepthEnabled(), ClearStencil(), ClearStencilEnabled()
{
this->set_ClearColorEnabled(clearColorEnabled);
this->set_ClearColor(clearColor);
this->set_ClearDepthEnabled(clearDepthEnabled);
this->set_ClearDepth(clearDepth);
this->set_ClearStencilEnabled(clearStencilEnabled);
this->set_ClearStencil(clearStencil);
}

