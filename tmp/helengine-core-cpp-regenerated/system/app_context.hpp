#ifndef APP_CONTEXT_HPP
#define APP_CONTEXT_HPP

#include <filesystem>
#include <stdexcept>
#include <string>
#include <system_error>

#ifdef _WIN32
#include <Windows.h>
#endif

/// Resolves executable-relative application context values used by generated runtime initialization.
class AppContext {
public:
    inline static std::string BaseDirectory = []() {
#ifdef _WIN32
        wchar_t buffer[MAX_PATH];
        DWORD length = GetModuleFileNameW(nullptr, buffer, MAX_PATH);
        if (length == 0) {
            throw std::runtime_error("Failed to resolve the current executable path.");
        }

        return std::filesystem::path(buffer).parent_path().string();
#else
        std::error_code errorCode;
        std::filesystem::path executablePath = std::filesystem::read_symlink("/proc/self/exe", errorCode);
        if (errorCode) {
            throw std::runtime_error("Failed to resolve the current executable path.");
        }

        return executablePath.parent_path().string();
#endif
    }();
};

#endif // APP_CONTEXT_HPP
