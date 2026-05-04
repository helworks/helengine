#include "binary-reader.hpp"
#include <algorithm>
#include <cstring>  // For memcpy

BinaryReader::BinaryReader(Stream& s, bool isLittleEndian)
    : stream(s), littleEndian(isLittleEndian) {
}

void BinaryReader::SetEndianness(bool isLittleEndian) {
    littleEndian = isLittleEndian;
}

template<typename T>
T BinaryReader::Read() {
    static_assert(std::is_arithmetic_v<T>, "Only arithmetic types are supported.");
    uint8_t buffer[sizeof(T)];

    if (stream.Read(buffer, 0, sizeof(T)) != sizeof(T)) {
        throw std::runtime_error("Failed to read expected bytes");
    }

    T value = 0;
    if (littleEndian) {
        std::memcpy(&value, buffer, sizeof(T));
    }
    else {
        std::reverse(buffer, buffer + sizeof(T));
        std::memcpy(&value, buffer, sizeof(T));
    }

    return value;
}

std::vector<uint8_t> BinaryReader::ReadBytes(size_t count) {
    std::vector<uint8_t> buffer(count);
    if (stream.Read(buffer.data(), 0, count) != count) {
        throw std::runtime_error("Failed to read expected bytes");
    }
    return buffer;
}

uint8_t BinaryReader::ReadByte() {
    return static_cast<uint8_t>(stream.InternalReadByte());
}

int32_t BinaryReader::ReadInt32() { return Read<int32_t>(); }
uint32_t BinaryReader::ReadUInt32() { return Read<uint32_t>(); }
int16_t BinaryReader::ReadInt16() { return Read<int16_t>(); }
uint16_t BinaryReader::ReadUInt16() { return Read<uint16_t>(); }
int64_t BinaryReader::ReadInt64() { return Read<int64_t>(); }
uint64_t BinaryReader::ReadUInt64() { return Read<uint64_t>(); }
float BinaryReader::ReadSingle() { return Read<float>(); }
double BinaryReader::ReadDouble() { return Read<double>(); }

char* BinaryReader::ReadString() {
    uint32_t length = ReadUInt32();
    char* buffer = static_cast<char*>(std::malloc(length + 1));
    if (!buffer) {
        throw std::runtime_error("Memory allocation failed");
    }
    if (stream.Read(reinterpret_cast<uint8_t*>(buffer), 0, length) != length) {
        std::free(buffer);
        throw std::runtime_error("Failed to read expected bytes");
    }
    buffer[length] = '\0'; // Null-terminate the string
    return buffer;
}

void BinaryReader::Close() {
    stream.Close();
}
