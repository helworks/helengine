#pragma once

#include <cstdint>
#include <vector>

template<typename T>
class Stack {
public:
    int32_t Count = 0;

    void Push(const T& value) {
        items.push_back(value);
        Count = static_cast<int32_t>(items.size());
    }

    T Pop() {
        T value = items.back();
        items.pop_back();
        Count = static_cast<int32_t>(items.size());
        return value;
    }

    T Peek() const {
        return items.back();
    }

private:
    std::vector<T> items;
};
