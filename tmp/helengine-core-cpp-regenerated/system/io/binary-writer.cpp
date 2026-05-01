#include "binary-writer.hpp"
#include <algorithm>
#include <cstring>  // For memcpy

BinaryWriter::BinaryWriter(Stream& s, bool isLittleEndian)
    : stream(s), littleEndian(isLittleEndian) {
}

void BinaryWriter::SetEndianness(bool isLittleEndian) {
    littleEndian = isLittleEndian;
}

template<typename T>
void BinaryWriter::Write(T value) {
    static_assert(std::is_arithmetic_v<T>, "Only arithmetic types are supported.");
    uint8_t buffer[sizeof(T)];

    std::memcpy(buffer, &value, sizeof(T));
    if (!littleEndian) {
        std::reverse(buffer, buffer + sizeof(T));
    }

    stream.Write(buffer, 0, sizeof(T));
}

void BinaryWriter::WriteBytes(const std::vector<uint8_t>& data) {
    stream.Write(data.data(), 0, data.size());
}

void BinaryWriter::WriteByte(uint8_t value) {
    stream.InternalWriteByte(value);
}

void BinaryWriter::WriteInt32(int32_t value) { Write(value); }
void BinaryWriter::WriteUInt32(uint32_t value) { Write(value); }
void BinaryWriter::WriteInt16(int16_t value) { Write(value); }
void BinaryWriter::WriteUInt16(uint16_t value) { Write(value); }
void BinaryWriter::WriteInt64(int64_t value) { Write(value); }
void BinaryWriter::WriteUInt64(uint64_t value) { Write(value); }
void BinaryWriter::WriteFloat(float value) { Write(value); }
void BinaryWriter::WriteDouble(double value) { Write(value); }

void BinaryWriter::WriteString(const std::string& str) {
    WriteUInt32(static_cast<uint32_t>(str.size())); // Prefix with length
    stream.Write(reinterpret_cast<const uint8_t*>(str.data()), 0, str.size());
}

void BinaryWriter::Flush() {
    stream.Flush();
}

void BinaryWriter::Close() {
    stream.Close();
}
