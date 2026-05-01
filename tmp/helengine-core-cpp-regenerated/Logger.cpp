#ifdef DrawText
#undef DrawText
#endif
#include "Logger.hpp"
#include "runtime/native_string.hpp"
#include "LogEntry.hpp"
#include "runtime/native_datetime.hpp"
#include "system/diagnostics/debug.hpp"
#include "LogLevel.hpp"
#include "runtime/array.hpp"
#include "runtime/finally.hpp"
#include "runtime/native_cast.hpp"
#include "runtime/native_datetime.hpp"
#include "runtime/native_dictionary.hpp"
#include "runtime/native_disposable.hpp"
#include "runtime/native_enum.hpp"
#include "runtime/native_event.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_nullable.hpp"
#include "runtime/native_span.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_tuple.hpp"
#include "runtime/native_type.hpp"
#include "system/app_context.hpp"
#include "system/bit_converter.hpp"
#include "system/diagnostics/debug.hpp"
#include "system/io/file-stream.hpp"
#include "system/io/file.hpp"
#include "system/io/memory-stream.hpp"
#include "system/io/path.hpp"
#include "system/io/stream.hpp"
#include "system/math.hpp"
#include "system/number.hpp"
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"
#include "system/text/regular_expressions/regex.hpp"

::Event Logger::ErrorLogged;

::Event Logger::MessageLogged;

::Event Logger::WarningLogged;

void Logger::WriteError(std::string message)
{
Write(LogLevel::Error, message);
}

void Logger::WriteLine(std::string message)
{
Write(LogLevel::Info, message);
}

void Logger::WriteWarning(std::string message)
{
Write(LogLevel::Warning, message);
}

void Logger::Write(::LogLevel level, std::string message)
{
const std::string text = message;
::LogEntry entry = ::LogEntry(level, text, DateTime::UtcNow());
std::string output = text;
    if (level == LogLevel::Warning)
    {
output = std::string("Warning: ") + text;
    }
else     if (level == LogLevel::Error)
    {
output = std::string("Error: ") + text;
    }
System::Diagnostics::Debug::WriteLine(output);
MessageLogged.Invoke(entry);
    if (level == LogLevel::Warning)
    {
WarningLogged.Invoke(entry);
    }
else     if (level == LogLevel::Error)
    {
ErrorLogged.Invoke(entry);
    }
}

