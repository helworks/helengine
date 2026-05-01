#pragma once

#include <algorithm>
#include <cstdint>
#include <initializer_list>

template<typename T>
class Array {
public:
    int32_t Length;
    T* Data;

    Array()
        : Length(0), Data(nullptr) {
    }

    explicit Array(int32_t length)
        : Length(length), Data(length > 0 ? new T[length] : nullptr) {
    }

    Array(std::initializer_list<T> values)
        : Length(static_cast<int32_t>(values.size())), Data(values.size() > 0 ? new T[values.size()] : nullptr) {
        int32_t index = 0;
        for (const T& value : values) {
            Data[index++] = value;
        }
    }

    static Array<T>* Empty() {
        static Array<T> EmptyArray(0);
        return &EmptyArray;
    }

    static void Copy(const Array<T>* source, Array<T>* destination, int32_t length) {
        if (source == nullptr || destination == nullptr || length <= 0) {
            return;
        }

        int32_t copyLength = std::min(length, std::min(source->Length, destination->Length));
        for (int32_t index = 0; index < copyLength; index++) {
            (*destination)[index] = (*source)[index];
        }
    }

    T& operator[](int32_t index) {
        return Data[index];
    }

    const T& operator[](int32_t index) const {
        return Data[index];
    }

    T* begin() {
        return Data;
    }

    T* end() {
        return Data + Length;
    }

    const T* begin() const {
        return Data;
    }

    const T* end() const {
        return Data + Length;
    }
};

template<typename T>
T* begin(Array<T>* values) {
    return values == nullptr ? nullptr : values->begin();
}

template<typename T>
T* end(Array<T>* values) {
    return values == nullptr ? nullptr : values->end();
}

template<typename T>
const T* begin(const Array<T>* values) {
    return values == nullptr ? nullptr : values->begin();
}

template<typename T>
const T* end(const Array<T>* values) {
    return values == nullptr ? nullptr : values->end();
}
