#ifndef FILE_STREAM_HPP
#define FILE_STREAM_HPP

#include "stream.hpp"
#include "seek-origin.hpp"
#include "file-mode.hpp"
#include "file-access.hpp"
#include "file-share.hpp"
#include <cstdio>  // For std::FILE*
#include <string>
#include "file-mode.hpp"

class FileStream : public Stream {
private:
    std::FILE* file;
    size_t position;
    size_t length;

    void UpdateLength();  // Helper to update file size

public:
    FileStream(const char* path, FileMode mode);
    FileStream(const char* path, FileMode mode, FileAccess access, FileShare share);
    FileStream(const std::string& path, FileMode mode);
    FileStream(const std::string& path, FileMode mode, FileAccess access, FileShare share);
    ~FileStream() override;

    size_t Read(uint8_t* buffer, size_t offset, size_t count) override;
    void Write(const uint8_t* buffer, size_t offset, size_t count) override;
    size_t Seek(int64_t offset, SeekOrigin origin) override;
    void SetLength(size_t length) override;

    bool CanRead() const override;
    bool CanWrite() const override;
    bool CanSeek() const override;

    size_t Length() const override;
    size_t Position() const override;
    void SetPosition(size_t value) override;

    void InternalReserve(size_t count) override;
    void InternalWriteByte(uint8_t byte) override;
    int InternalReadByte() override;

    void Dispose() override;
    void Close() override;
    void Flush() override;
};

#endif // FILE_STREAM_HPP
