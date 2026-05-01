#include "file-stream.hpp"
#include <stdexcept>  // For exceptions
#include <cstring>    // For std::memcpy
#include <sys/stat.h> // For file size retrieval
#if defined(_WIN32)
#include <io.h>
#else
#include <unistd.h>
#endif

// Helper function to get file mode as C-style string
const char* GetFileMode(FileMode mode) {
    switch (mode) {
    case FileMode::Append: return "a+b";
    case FileMode::Create: return "w+b";
    case FileMode::CreateNew: return "wbx+";
    case FileMode::Open: return "rb";
    case FileMode::OpenOrCreate: return "r+b";
    case FileMode::Truncate: return "wb";
    default: throw std::runtime_error("Invalid FileMode");
    }
}

// Constructor
FileStream::FileStream(const char* path, FileMode mode) : file(nullptr), position(0), length(0) {
    file = std::fopen(path, GetFileMode(mode));
    if (!file) {
        throw std::runtime_error(std::string("Failed to open file: ") + path);
    }

    UpdateLength();
}

FileStream::FileStream(const char* path, FileMode mode, FileAccess, FileShare)
    : FileStream(path, mode) {
}

FileStream::FileStream(const std::string& path, FileMode mode)
    : FileStream(path.c_str(), mode) {
}

FileStream::FileStream(const std::string& path, FileMode mode, FileAccess access, FileShare share)
    : FileStream(path.c_str(), mode, access, share) {
}

// Destructor
FileStream::~FileStream() {
    Close();
}

// Reads data from file
size_t FileStream::Read(uint8_t* buffer, size_t offset, size_t count) {
    if (!CanRead() || !buffer) return 0;
    std::fseek(file, position, SEEK_SET);

    size_t bytesRead = std::fread(buffer + offset, 1, count, file);
    position += bytesRead;
    return bytesRead;
}

// Writes data to file
void FileStream::Write(const uint8_t* buffer, size_t offset, size_t count) {
    if (!CanWrite() || !buffer) return;
    std::fseek(file, position, SEEK_SET);

    size_t bytesWritten = std::fwrite(buffer + offset, 1, count, file);
    position += bytesWritten;
    UpdateLength();
}

// Seeks to a position in file
size_t FileStream::Seek(int64_t offset, SeekOrigin origin) {
    if (!CanSeek()) return position;

    int seekMode;
    switch (origin) {
    case SeekOrigin::Begin: seekMode = SEEK_SET; break;
    case SeekOrigin::Current: seekMode = SEEK_CUR; break;
    case SeekOrigin::End: seekMode = SEEK_END; break;
    }

    std::fseek(file, offset, seekMode);
    position = std::ftell(file);
    return position;
}

// Truncates or extends the file
void FileStream::SetLength(size_t newLength) {
    if (!file) return;
    std::fflush(file);
#if defined(_WIN32)
    _chsize_s(fileno(file), newLength);
#else
    ftruncate(fileno(file), newLength);
#endif
    UpdateLength();
}

// Updates the stored file length
void FileStream::UpdateLength() {
    if (!file) return;
    struct stat fileStat;
    if (fstat(fileno(file), &fileStat) == 0) {
        length = fileStat.st_size;
    }
}

// Properties
bool FileStream::CanRead() const { return file != nullptr; }
bool FileStream::CanWrite() const { return file != nullptr; }
bool FileStream::CanSeek() const { return file != nullptr; }

size_t FileStream::Length() const { return length; }
size_t FileStream::Position() const { return position; }
void FileStream::SetPosition(size_t value) { position = std::min(value, length); }

// Internal byte-level operations
void FileStream::InternalReserve(size_t count) { /* Not needed for file streams */ }

void FileStream::InternalWriteByte(uint8_t byte) {
    Write(&byte, 0, 1);
}

int FileStream::InternalReadByte() {
    uint8_t byte;
    return (Read(&byte, 0, 1) > 0) ? byte : -1;
}

// Flushes the file buffer
void FileStream::Flush() {
    if (file) std::fflush(file);
}

// Closes the file
void FileStream::Close() {
    if (file) {
        std::fclose(file);
        file = nullptr;
    }
}

// Cleanup function
void FileStream::Dispose() {
    Close();
}
