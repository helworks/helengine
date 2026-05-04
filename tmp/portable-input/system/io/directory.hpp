#pragma once

#include <string>

class Directory {
public:
    static bool Exists(const std::string& path);
    static void CreateDirectory(const std::string& path);
};
