#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class LogEntry;

#include "runtime/native_string.hpp"
#include "LogEntry.hpp"
#include "LogEntry.hpp"
#include "runtime/native_datetime.hpp"
#include "runtime/native_datetime.hpp"
#include "system/diagnostics/debug.hpp"
#include "system/diagnostics/debug.hpp"
#include "runtime/native_event.hpp"
#include "LogLevel.hpp"

class Logger
{
public:
    virtual ~Logger() = default;

    static ::Event ErrorLogged;

    static ::Event MessageLogged;

    static ::Event WarningLogged;

    static void WriteError(std::string message);

    static void WriteLine(std::string message);

    static void WriteWarning(std::string message);
private:
    static void Write(::LogLevel level, std::string message);
};
