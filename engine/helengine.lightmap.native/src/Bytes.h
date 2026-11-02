#pragma once
#include "Internal.h"
#include <cstring>
#include <limits>
namespace hellm::detail {
/// <summary>Canonical little-endian serializer with bounded checked reads.</summary>
class Bytes {
  public:
    /// <summary>Canonical little-endian payload or validated file bytes.</summary>
    std::vector<uint8_t> Data;
    /// <summary>Next checked read position within Data.</summary>
    size_t Position = 0;
    /// <summary>Append an unsigned 32-bit integer in canonical little-endian order.</summary>
    void U32(uint32_t x) {
        for (int i = 0; i < 4; ++i)
            Data.push_back(uint8_t(x >> (8 * i)));
    }
    /// <summary>Append an unsigned 64-bit integer in canonical little-endian order.</summary>
    void U64(uint64_t x) {
        for (int i = 0; i < 8; ++i)
            Data.push_back(uint8_t(x >> (8 * i)));
    }
    /// <summary>Append the exact IEEE binary32 bits in canonical little-endian order.</summary>
    void F32(float x) {
        uint32_t u;
        std::memcpy(&u, &x, 4);
        U32(u);
    }
    /// <summary>Append both texture coordinates without altering their representation.</summary>
    void V2(Vec2 v) {
        F32(v.X);
        F32(v.Y);
    }
    /// <summary>Append all world-space or linear RGB vector components in order.</summary>
    void V3(Vec3 v) {
        F32(v.X);
        F32(v.Y);
        F32(v.Z);
    }
    /// <summary>Append a bounded length-prefixed stable mesh key.</summary>
    void String(const std::string &s) {
        if (s.size() > 1048576)
            throw std::runtime_error("Binding key too long");
        U32(uint32_t(s.size()));
        Data.insert(Data.end(), s.begin(), s.end());
    }
    /// <summary>Read a checked canonical unsigned 32-bit integer.</summary>
    uint32_t Read32() {
        Need(4);
        uint32_t n = 0;
        for (int i = 0; i < 4; ++i)
            n |= uint32_t(Data[Position++]) << (i * 8);
        return n;
    }
    /// <summary>Read a checked canonical unsigned 64-bit integer.</summary>
    uint64_t Read64() {
        Need(8);
        uint64_t n = 0;
        for (int i = 0; i < 8; ++i)
            n |= uint64_t(Data[Position++]) << (i * 8);
        return n;
    }
    /// <summary>Read the exact stored IEEE binary32 bits.</summary>
    float ReadFloat() {
        auto u = Read32();
        float v;
        std::memcpy(&v, &u, 4);
        return v;
    }
    /// <summary>Decode a checked canonical float array in bulk, preserving exact bits on little-endian hosts.</summary>
    void ReadFloats(std::vector<float> &values, uint32_t count) {
        static_assert(sizeof(float) == 4 && std::numeric_limits<float>::is_iec559,
                      "Lightmap format requires IEEE binary32 floats");
        const size_t byteCount = size_t(count) * sizeof(float);
        Need(byteCount);
        values.resize(count);
        const uint8_t *source = Data.data() + Position;
        const uint32_t endianProbe = 1;
        if (*reinterpret_cast<const uint8_t *>(&endianProbe) == 1) {
            std::memcpy(values.data(), source, byteCount);
        } else {
            float *destination = values.data();
            for (uint32_t i = 0; i < count; ++i) {
                const uint8_t *bytes = source + size_t(i) * 4;
                const uint32_t bits = uint32_t(bytes[0]) | (uint32_t(bytes[1]) << 8) | (uint32_t(bytes[2]) << 16) |
                                      (uint32_t(bytes[3]) << 24);
                std::memcpy(destination + i, &bits, sizeof(float));
            }
        }
        Position += byteCount;
    }
    /// <summary>Read the next two-component texture coordinate.</summary>
    Vec2 Read2() {
        Vec2 v;
        v.X = ReadFloat();
        v.Y = ReadFloat();
        return v;
    }
    /// <summary>Read the next three-component world-space or linear RGB value.</summary>
    Vec3 Read3() {
        Vec3 v;
        v.X = ReadFloat();
        v.Y = ReadFloat();
        v.Z = ReadFloat();
        return v;
    }
    /// <summary>Read a bounded stable mesh key, rejecting truncated payloads.</summary>
    std::string ReadString() {
        auto n = Count(1);
        if (n > 1048576)
            throw std::runtime_error("Oversized binding key");
        std::string s(Data.begin() + Position, Data.begin() + Position + n);
        Position += n;
        return s;
    }
    /// <summary>Read a collection count bounded by the remaining payload bytes and element size.</summary>
    uint32_t Count(size_t stride) {
        auto n = Read32();
        if (uint64_t(n) * stride > Data.size() - Position)
            throw std::runtime_error("Corrupt asset count");
        return n;
    }
    /// <summary>Reject any read that would extend beyond the validated payload.</summary>
    void Need(size_t n) {
        if (n > Data.size() - Position)
            throw std::runtime_error("Truncated lightmap asset");
    }
    /// <summary>Reject unexpected trailing fields after the supported asset payload.</summary>
    void End() {
        if (Position != Data.size())
            throw std::runtime_error("Unexpected trailing asset data");
    }
};
} // namespace hellm::detail
