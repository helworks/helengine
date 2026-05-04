#ifndef FILE_SHARE_HPP
#define FILE_SHARE_HPP

#include <cstdint>

enum class FileShare : uint8_t {
    None,
    Read,
    Write,
    ReadWrite
};

#endif // FILE_SHARE_HPP
