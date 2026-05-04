#include "memory-stream.hpp"
#include <algorithm>  // For std::min
#include <cstring>    // For std::memcpy

// Constructor
MemoryStream::MemoryStream() : buffer(), position(0), writable(true) {}

MemoryStream::MemoryStream(Array<uint8_t>* data, bool writable)
    : buffer(), position(0), writable(writable) {
    if (data != nullptr && data->Length > 0 && data->Data != nullptr) {
        buffer.assign(data->Data, data->Data + data->Length);
    }
}

// Read data into a buffer
size_t MemoryStream::Read(uint8_t* outBuffer, size_t offset, size_t count) {
    if (!CanRead() || !outBuffer) return 0;

    size_t readable = std::min(count, buffer.size() - position);
    std::memcpy(outBuffer + offset, buffer.data() + position, readable);
    position += readable;
    return readable;
}

// Write data from a buffer
void MemoryStream::Write(const uint8_t* inBuffer, size_t offset, size_t count) {
    if (!CanWrite() || !inBuffer) return;

    if (position + count > buffer.size()) {
        buffer.resize(position + count);
    }

    std::memcpy(buffer.data() + position, inBuffer + offset, count);
    position += count;
}

// Seek to a specific position
size_t MemoryStream::Seek(int64_t offset, SeekOrigin origin) {
    if (!CanSeek()) return position;

    switch (origin) {
    case SeekOrigin::Begin:
        position = offset;
        break;
    case SeekOrigin::Current:
        position += offset;
        break;
    case SeekOrigin::End:
        position = buffer.size() + offset;
        break;
    }

    position = std::min(position, buffer.size());  // Ensure position is valid
    return position;
}

// Set the buffer length
void MemoryStream::SetLength(size_t length) {
    buffer.resize(length);
    if (position > length) position = length;
}

// Properties
bool MemoryStream::CanRead() const { return true; }
bool MemoryStream::CanWrite() const { return writable; }
bool MemoryStream::CanSeek() const { return true; }

size_t MemoryStream::Length() const { return buffer.size(); }
size_t MemoryStream::Position() const { return position; }
void MemoryStream::SetPosition(size_t value) {
    position = std::min(value, buffer.size());
}

// Internal reserve function
void MemoryStream::InternalReserve(size_t count) {
    buffer.reserve(count);
}

// Write a single byte
void MemoryStream::InternalWriteByte(uint8_t byte) {
    if (position >= buffer.size()) {
        buffer.resize(position + 1);
    }
    buffer[position++] = byte;
}

// Read a single byte
int MemoryStream::InternalReadByte() {
    return (position < buffer.size()) ? buffer[position++] : -1;
}

Array<uint8_t>* MemoryStream::ToArray() {
    Array<uint8_t>* data = new Array<uint8_t>(static_cast<int32_t>(buffer.size()));
    if (data->Length > 0 && data->Data != nullptr) {
        std::memcpy(data->Data, buffer.data(), buffer.size());
    }

    return data;
}
