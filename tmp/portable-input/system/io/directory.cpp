#include "directory.hpp"

#include <filesystem>

bool Directory::Exists(const std::string& path) {
    if (path.empty()) {
        return false;
    }

    std::error_code errorCode;
    return std::filesystem::is_directory(path, errorCode);
}

void Directory::CreateDirectory(const std::string& path) {
    if (path.empty()) {
        return;
    }

    std::error_code errorCode;
    std::filesystem::create_directories(path, errorCode);
}
