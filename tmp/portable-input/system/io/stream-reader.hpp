#ifndef STREAM_READER_HPP
#define STREAM_READER_HPP

#include <cstdint>
#include <stdexcept>
#include <string>

#include "stream.hpp"
#include "../text/encoding.hpp"

class StreamReader {
private:
    Stream* stream;
    bool leaveOpen;
    bool disposed;

public:
    StreamReader(Stream* sourceStream, const Encoding&, bool, int32_t, bool leaveOpenStream)
        : stream(sourceStream), leaveOpen(leaveOpenStream), disposed(false) {
        if (stream == nullptr) {
            throw std::invalid_argument("sourceStream");
        }
    }

    ~StreamReader() {
        Dispose();
    }

    std::string ReadToEnd() {
        std::string result;
        uint8_t buffer[4096];

        while (true) {
            size_t bytesRead = stream->Read(buffer, 0, sizeof(buffer));
            if (bytesRead == 0) {
                break;
            }

            result.append(reinterpret_cast<const char*>(buffer), bytesRead);
        }

        return result;
    }

    void Dispose() {
        if (!disposed && !leaveOpen && stream != nullptr) {
            stream->Dispose();
        }

        disposed = true;
    }
};

#endif
