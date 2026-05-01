#pragma once

#include <cstdint>

/// <summary>
/// Provides the minimal managed BinaryPrimitives surface required by transpiled serializer code.
/// </summary>
class BinaryPrimitives {
public:
    /// <summary>
    /// Reads an unsigned 16-bit integer from a little-endian byte buffer.
    /// </summary>
    static uint16_t ReadUInt16LittleEndian(const uint8_t* buffer) {
        return static_cast<uint16_t>(buffer[0]) |
            static_cast<uint16_t>(buffer[1] << 8);
    }

    /// <summary>
    /// Reads a signed 32-bit integer from a little-endian byte buffer.
    /// </summary>
    static int32_t ReadInt32LittleEndian(const uint8_t* buffer) {
        return static_cast<int32_t>(
            static_cast<uint32_t>(buffer[0]) |
            (static_cast<uint32_t>(buffer[1]) << 8) |
            (static_cast<uint32_t>(buffer[2]) << 16) |
            (static_cast<uint32_t>(buffer[3]) << 24));
    }

    /// <summary>
    /// Reads an unsigned 32-bit integer from a little-endian byte buffer.
    /// </summary>
    static uint32_t ReadUInt32LittleEndian(const uint8_t* buffer) {
        return static_cast<uint32_t>(buffer[0]) |
            (static_cast<uint32_t>(buffer[1]) << 8) |
            (static_cast<uint32_t>(buffer[2]) << 16) |
            (static_cast<uint32_t>(buffer[3]) << 24);
    }

    /// <summary>
    /// Reads a signed 64-bit integer from a little-endian byte buffer.
    /// </summary>
    static int64_t ReadInt64LittleEndian(const uint8_t* buffer) {
        return static_cast<int64_t>(
            static_cast<uint64_t>(buffer[0]) |
            (static_cast<uint64_t>(buffer[1]) << 8) |
            (static_cast<uint64_t>(buffer[2]) << 16) |
            (static_cast<uint64_t>(buffer[3]) << 24) |
            (static_cast<uint64_t>(buffer[4]) << 32) |
            (static_cast<uint64_t>(buffer[5]) << 40) |
            (static_cast<uint64_t>(buffer[6]) << 48) |
            (static_cast<uint64_t>(buffer[7]) << 56));
    }

    /// <summary>
    /// Reads an unsigned 16-bit integer from a big-endian byte buffer.
    /// </summary>
    static uint16_t ReadUInt16BigEndian(const uint8_t* buffer) {
        return static_cast<uint16_t>(buffer[1]) |
            static_cast<uint16_t>(buffer[0] << 8);
    }

    /// <summary>
    /// Reads a signed 32-bit integer from a big-endian byte buffer.
    /// </summary>
    static int32_t ReadInt32BigEndian(const uint8_t* buffer) {
        return static_cast<int32_t>(
            static_cast<uint32_t>(buffer[3]) |
            (static_cast<uint32_t>(buffer[2]) << 8) |
            (static_cast<uint32_t>(buffer[1]) << 16) |
            (static_cast<uint32_t>(buffer[0]) << 24));
    }

    /// <summary>
    /// Reads an unsigned 32-bit integer from a big-endian byte buffer.
    /// </summary>
    static uint32_t ReadUInt32BigEndian(const uint8_t* buffer) {
        return static_cast<uint32_t>(buffer[3]) |
            (static_cast<uint32_t>(buffer[2]) << 8) |
            (static_cast<uint32_t>(buffer[1]) << 16) |
            (static_cast<uint32_t>(buffer[0]) << 24);
    }

    /// <summary>
    /// Reads a signed 64-bit integer from a big-endian byte buffer.
    /// </summary>
    static int64_t ReadInt64BigEndian(const uint8_t* buffer) {
        return static_cast<int64_t>(
            static_cast<uint64_t>(buffer[7]) |
            (static_cast<uint64_t>(buffer[6]) << 8) |
            (static_cast<uint64_t>(buffer[5]) << 16) |
            (static_cast<uint64_t>(buffer[4]) << 24) |
            (static_cast<uint64_t>(buffer[3]) << 32) |
            (static_cast<uint64_t>(buffer[2]) << 40) |
            (static_cast<uint64_t>(buffer[1]) << 48) |
            (static_cast<uint64_t>(buffer[0]) << 56));
    }

    /// <summary>
    /// Writes an unsigned 16-bit integer into a little-endian byte buffer.
    /// </summary>
    static void WriteUInt16LittleEndian(uint8_t* buffer, uint16_t value) {
        buffer[0] = static_cast<uint8_t>(value & 0xFF);
        buffer[1] = static_cast<uint8_t>((value >> 8) & 0xFF);
    }

    /// <summary>
    /// Writes a signed 32-bit integer into a little-endian byte buffer.
    /// </summary>
    static void WriteInt32LittleEndian(uint8_t* buffer, int32_t value) {
        WriteUInt32LittleEndian(buffer, static_cast<uint32_t>(value));
    }

    /// <summary>
    /// Writes an unsigned 32-bit integer into a little-endian byte buffer.
    /// </summary>
    static void WriteUInt32LittleEndian(uint8_t* buffer, uint32_t value) {
        buffer[0] = static_cast<uint8_t>(value & 0xFF);
        buffer[1] = static_cast<uint8_t>((value >> 8) & 0xFF);
        buffer[2] = static_cast<uint8_t>((value >> 16) & 0xFF);
        buffer[3] = static_cast<uint8_t>((value >> 24) & 0xFF);
    }

    /// <summary>
    /// Writes a signed 64-bit integer into a little-endian byte buffer.
    /// </summary>
    static void WriteInt64LittleEndian(uint8_t* buffer, int64_t value) {
        WriteUInt64LittleEndian(buffer, static_cast<uint64_t>(value));
    }

    /// <summary>
    /// Writes an unsigned 16-bit integer into a big-endian byte buffer.
    /// </summary>
    static void WriteUInt16BigEndian(uint8_t* buffer, uint16_t value) {
        buffer[0] = static_cast<uint8_t>((value >> 8) & 0xFF);
        buffer[1] = static_cast<uint8_t>(value & 0xFF);
    }

    /// <summary>
    /// Writes a signed 32-bit integer into a big-endian byte buffer.
    /// </summary>
    static void WriteInt32BigEndian(uint8_t* buffer, int32_t value) {
        WriteUInt32BigEndian(buffer, static_cast<uint32_t>(value));
    }

    /// <summary>
    /// Writes an unsigned 32-bit integer into a big-endian byte buffer.
    /// </summary>
    static void WriteUInt32BigEndian(uint8_t* buffer, uint32_t value) {
        buffer[0] = static_cast<uint8_t>((value >> 24) & 0xFF);
        buffer[1] = static_cast<uint8_t>((value >> 16) & 0xFF);
        buffer[2] = static_cast<uint8_t>((value >> 8) & 0xFF);
        buffer[3] = static_cast<uint8_t>(value & 0xFF);
    }

    /// <summary>
    /// Writes a signed 64-bit integer into a big-endian byte buffer.
    /// </summary>
    static void WriteInt64BigEndian(uint8_t* buffer, int64_t value) {
        WriteUInt64BigEndian(buffer, static_cast<uint64_t>(value));
    }

private:
    /// <summary>
    /// Writes an unsigned 64-bit integer into a little-endian byte buffer.
    /// </summary>
    static void WriteUInt64LittleEndian(uint8_t* buffer, uint64_t value) {
        buffer[0] = static_cast<uint8_t>(value & 0xFF);
        buffer[1] = static_cast<uint8_t>((value >> 8) & 0xFF);
        buffer[2] = static_cast<uint8_t>((value >> 16) & 0xFF);
        buffer[3] = static_cast<uint8_t>((value >> 24) & 0xFF);
        buffer[4] = static_cast<uint8_t>((value >> 32) & 0xFF);
        buffer[5] = static_cast<uint8_t>((value >> 40) & 0xFF);
        buffer[6] = static_cast<uint8_t>((value >> 48) & 0xFF);
        buffer[7] = static_cast<uint8_t>((value >> 56) & 0xFF);
    }

    /// <summary>
    /// Writes an unsigned 64-bit integer into a big-endian byte buffer.
    /// </summary>
    static void WriteUInt64BigEndian(uint8_t* buffer, uint64_t value) {
        buffer[0] = static_cast<uint8_t>((value >> 56) & 0xFF);
        buffer[1] = static_cast<uint8_t>((value >> 48) & 0xFF);
        buffer[2] = static_cast<uint8_t>((value >> 40) & 0xFF);
        buffer[3] = static_cast<uint8_t>((value >> 32) & 0xFF);
        buffer[4] = static_cast<uint8_t>((value >> 24) & 0xFF);
        buffer[5] = static_cast<uint8_t>((value >> 16) & 0xFF);
        buffer[6] = static_cast<uint8_t>((value >> 8) & 0xFF);
        buffer[7] = static_cast<uint8_t>(value & 0xFF);
    }
};
