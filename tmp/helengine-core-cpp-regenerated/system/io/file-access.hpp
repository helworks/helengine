#ifndef FILE_ACCESS_HPP
#define FILE_ACCESS_HPP

#include <cstdint>

enum class FileAccess : uint8_t {
    Read,
    Write,
    ReadWrite
};

#endif // FILE_ACCESS_HPP
