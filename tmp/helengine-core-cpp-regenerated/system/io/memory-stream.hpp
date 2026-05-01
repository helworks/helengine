#ifndef MEMORY_STREAM_HPP
#define MEMORY_STREAM_HPP

#include "stream.hpp"
#include "../../runtime/array.hpp"
#include <vector>
#include <cstddef>  // For size_t
#include <cstdint>  // For uint8_t

class MemoryStream : public Stream {
private:
    std::vector<uint8_t> buffer;
    size_t position = 0;
    bool writable = true;

public:
    // Constructor & Destructor
    MemoryStream();
    MemoryStream(Array<uint8_t>* data, bool writable);
    ~MemoryStream() override = default;

    // Stream Implementation
    size_t Read(uint8_t* outBuffer, size_t offset, size_t count) override;
    void Write(const uint8_t* inBuffer, size_t offset, size_t count) override;
    size_t Seek(int64_t offset, SeekOrigin origin) override;
    void SetLength(size_t length) override;

    // Properties
    bool CanRead() const override;
    bool CanWrite() const override;
    bool CanSeek() const override;

    size_t Length() const override;
    size_t Position() const override;
    void SetPosition(size_t value) override;

    // Internal Methods
    void InternalReserve(size_t count) override;
    void InternalWriteByte(uint8_t byte) override;
    int InternalReadByte() override;
    Array<uint8_t>* ToArray();

    // Stream Management
    void Flush() override {}
    void Close() override {}
    void Dispose() override {}
};

#endif // MEMORY_STREAM_HPP
