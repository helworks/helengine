#ifdef DrawText
#undef DrawText
#endif
#include "LogEntry.hpp"
#include "runtime/array.hpp"
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
#include "system/guid.hpp"
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

LogEntry::LogEntry() : Level(), Message(), Timestamp()
{
}

::LogLevel LogEntry::get_Level()
{
return this->Level;
}

std::string LogEntry::get_Message()
{
return this->Message;
}

DateTime LogEntry::get_Timestamp()
{
return this->Timestamp;
}

LogEntry::LogEntry(::LogLevel level, std::string message, DateTime timestamp) : Level(), Message(), Timestamp()
{
this->Level = level;
this->Message = message;
this->Timestamp = timestamp;
}

