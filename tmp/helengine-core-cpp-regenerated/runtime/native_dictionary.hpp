#pragma once

#include <cstdint>
#include <unordered_map>
#include <vector>

#include "native_string.hpp"

class StringComparer;

template<typename TKey, typename TValue>
class Dictionary : public std::unordered_map<TKey, TValue> {
public:
    using std::unordered_map<TKey, TValue>::unordered_map;

    explicit Dictionary(const StringComparer&) {
    }

    void Add(const TKey& key, const TValue& value) {
        this->insert_or_assign(key, value);
    }

    bool ContainsKey(const TKey& key) const {
        return this->find(key) != this->end();
    }

    bool Remove(const TKey& key) {
        return this->erase(key) > 0;
    }

    bool TryGetValue(const TKey& key, TValue& value) const {
        auto iterator = this->find(key);
        if (iterator == this->end()) {
            return false;
        }

        value = iterator->second;
        return true;
    }

    std::vector<TKey> Keys() const {
        std::vector<TKey> keys;
        keys.reserve(this->size());
        for (const auto& pair : *this) {
            keys.push_back(pair.first);
        }

        return keys;
    }

    int32_t Count() const {
        return static_cast<int32_t>(this->size());
    }
};
