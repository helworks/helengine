#pragma once

#include "runtime/native_string.hpp"

namespace System {
namespace Diagnostics {
class Debug {
public:
    static void WriteLine(const std::string& text) {
        (void)text;
    }
};
}
}
