#ifndef STREAM_HPP
#define STREAM_HPP

#include <cstdint>  // For uint8_t
#include <stdexcept>
#include "../../runtime/array.hpp"
#include "../../runtime/native_span.hpp"
#include "seek-origin.hpp"  // Assuming this exists like in your TypeScript

class Stream {
public:
    virtual ~Stream() = default;

    // Abstract methods
    virtual size_t Read(uint8_t* buffer, size_t offset, size_t count) = 0;
    virtual void Write(const uint8_t* buffer, size_t offset, size_t count) = 0;
    virtual size_t Seek(int64_t offset, SeekOrigin origin) = 0;
    virtual void SetLength(size_t length) = 0;

    virtual size_t Read(Span<uint8_t> buffer) {
        return Read(buffer.Data, 0, buffer.Length);
    }

    virtual size_t Read(Array<uint8_t>* buffer, size_t offset, size_t count) {
        return buffer == nullptr
            ? 0
            : Read(buffer->Data, offset, count);
    }

    virtual void Write(Span<uint8_t> buffer) {
        Write(buffer.Data, 0, buffer.Length);
    }

    virtual void Write(Array<uint8_t>* buffer, size_t offset, size_t count) {
        if (buffer == nullptr) {
            return;
        }

        Write(buffer->Data, offset, count);
    }

    // Properties
    virtual bool CanRead() const = 0;
    virtual bool CanWrite() const = 0;
    virtual bool CanSeek() const = 0;
    virtual size_t Length() const = 0;
    virtual size_t Position() const = 0;
    virtual void SetPosition(size_t value) = 0;

    // Default Implementation for Timeout (Exception-like behavior)
    virtual bool CanTimeout() const { return false; }

    virtual size_t ReadTimeout() const {
        throw std::runtime_error("Timeout not supported");
    }

    virtual void SetReadTimeout(size_t) {
        throw std::runtime_error("Timeout not supported");
    }

    virtual size_t WriteTimeout() const {
        throw std::runtime_error("Timeout not supported");
    }

    virtual void SetWriteTimeout(size_t) {
        throw std::runtime_error("Timeout not supported");
    }

    virtual void InternalReserve(size_t count) = 0;
    virtual void InternalWriteByte(uint8_t byte) = 0;
    virtual int InternalReadByte() = 0;
    virtual void WriteByte(uint8_t byte) { InternalWriteByte(byte); }
    virtual int ReadByte() { return InternalReadByte(); }

    virtual void Dispose() {}
    virtual void Close() {}
    virtual void Flush() {}

    virtual void CopyTo(Stream* destination) {
        if (destination == nullptr) {
            return;
        }

        Array<uint8_t> buffer(81920);
        while (true) {
            size_t bytesRead = Read(buffer.Data, 0, static_cast<size_t>(buffer.Length));
            if (bytesRead == 0) {
                break;
            }

            destination->Write(buffer.Data, 0, bytesRead);
        }
    }
};

#endif // STREAM_HPP
