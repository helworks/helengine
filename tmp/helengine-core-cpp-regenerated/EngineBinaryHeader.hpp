#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

#include "EngineBinaryEndianness.hpp"

class EngineBinaryHeader
{
public:
    virtual ~EngineBinaryHeader() = default;

    ::EngineBinaryEndianness Endianness;

    ::EngineBinaryEndianness get_Endianness();

    uint16_t FormatId;

    uint16_t get_FormatId();

    uint16_t RecordKind;

    uint16_t get_RecordKind();

    uint16_t ValueKind;

    uint16_t get_ValueKind();

    uint8_t Version;

    uint8_t get_Version();

    EngineBinaryHeader(::EngineBinaryEndianness endianness, uint8_t version, uint16_t formatId, uint16_t recordKind, uint16_t valueKind);
};
