#ifndef ACTION_TPP
#define ACTION_TPP

#include "action.hpp"

// Function pointer constructor
template<typename... TArgs>
Action<TArgs...>::Action(FuncType f) : func(f) {}

// Invoke function
template<typename... TArgs>
void Action<TArgs...>::operator()(TArgs... args) const {
    if (func) {
        func(args...);
    }
}

// Check if Action is valid
template<typename... TArgs>
Action<TArgs...>::operator bool() const {
    return func != nullptr;
}

#endif // ACTION_TPP
