#ifndef STRING_READER_HPP
#define STRING_READER_HPP

#include <cstddef>
#include <string>

class StringReaderLine {
private:
    bool hasValue;
    std::string value;

public:
    StringReaderLine()
        : hasValue(false), value() {
    }

    StringReaderLine(const std::string& lineValue, bool hasLine)
        : hasValue(hasLine), value(lineValue) {
    }

    bool operator==(std::nullptr_t) const {
        return !hasValue;
    }

    bool operator!=(std::nullptr_t) const {
        return hasValue;
    }

    operator const std::string&() const {
        return value;
    }
};

class StringReader {
private:
    std::string source;
    size_t position;

public:
    explicit StringReader(const std::string& text)
        : source(text), position(0) {
    }

    StringReaderLine ReadLine() {
        if (position >= source.size()) {
            return StringReaderLine(std::string(), false);
        }

        size_t lineEnd = source.find_first_of("\r\n", position);
        if (lineEnd == std::string::npos) {
            std::string line = source.substr(position);
            position = source.size();
            return StringReaderLine(line, true);
        }

        std::string line = source.substr(position, lineEnd - position);
        position = lineEnd + 1;

        if (position < source.size() &&
            source[lineEnd] == '\r' &&
            source[position] == '\n') {
            position++;
        }

        return StringReaderLine(line, true);
    }

    void Dispose() {
    }
};

#endif
