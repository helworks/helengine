#include "path.hpp"

#include <filesystem>

std::string Path::Combine(const std::string& left, const std::string& right) {
    if (left.empty()) {
        return right;
    }

    if (right.empty()) {
        return left;
    }

    return (std::filesystem::path(left) / right).lexically_normal().string();
}

std::string Path::Combine(const std::string& first, const std::string& second, const std::string& third) {
    return Combine(Combine(first, second), third);
}

std::string Path::GetDirectoryName(const std::string& path) {
    if (path.empty()) {
        return std::string();
    }

    return std::filesystem::path(path).parent_path().string();
}

std::string Path::GetFileName(const std::string& path) {
    if (path.empty()) {
        return std::string();
    }

    return std::filesystem::path(path).filename().string();
}

std::string Path::GetFullPath(const std::string& path) {
    if (path.empty()) {
        return std::filesystem::current_path().string();
    }

    return std::filesystem::absolute(std::filesystem::path(path)).lexically_normal().string();
}

bool Path::IsPathRooted(const std::string& path) {
    if (path.empty()) {
        return false;
    }

    return std::filesystem::path(path).is_absolute();
}
