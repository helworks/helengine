#ifndef PATH_HPP
#define PATH_HPP

#include <string>

class Path {
public:
    #ifdef _WIN32
    static constexpr char DirectorySeparatorChar = '\\';
    static constexpr char AltDirectorySeparatorChar = '/';
    #else
    static constexpr char DirectorySeparatorChar = '/';
    static constexpr char AltDirectorySeparatorChar = '\\';
    #endif

    static std::string Combine(const std::string& left, const std::string& right);

    static std::string Combine(const std::string& first, const std::string& second, const std::string& third);

    static std::string GetDirectoryName(const std::string& path);

    static std::string GetFileName(const std::string& path);

    static std::string GetFullPath(const std::string& path);

    static bool IsPathRooted(const std::string& path);
};

#endif // PATH_HPP
