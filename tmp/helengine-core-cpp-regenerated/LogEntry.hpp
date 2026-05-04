#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

#include "LogLevel.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_datetime.hpp"

class LogEntry
{
public:
    virtual ~LogEntry() = default;

    LogEntry();

    ::LogLevel Level;

    ::LogLevel get_Level();

    std::string Message;

    std::string get_Message();

    DateTime Timestamp;

    DateTime get_Timestamp();

    LogEntry(::LogLevel level, std::string message, DateTime timestamp);
};
