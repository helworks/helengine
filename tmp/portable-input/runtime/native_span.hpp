#pragma once

#include <cstddef>
#include "array.hpp"

template <typename T>
class Span {
public:
    T* Data;
    size_t Length;

    Span()
        : Data(nullptr),
          Length(0) {
    }

    Span(T* data, size_t length)
        : Data(data),
          Length(length) {
    }

    Span(Array<T>* array)
        : Data(array != nullptr ? array->Data : nullptr),
          Length(array != nullptr ? static_cast<size_t>(array->Length) : 0) {
    }

    template <size_t N>
    Span(T (&buffer)[N])
        : Data(buffer),
          Length(N) {
    }

    Span Slice(size_t offset) const {
        return Span(Data + offset, Length - offset);
    }
};
