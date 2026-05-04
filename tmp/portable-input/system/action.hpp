#ifndef ACTION_HPP
#define ACTION_HPP

template<typename... TArgs>
class Action {
private:
    using FuncType = void(*)(TArgs...);
    FuncType func = nullptr;

public:
    // Default constructor
    Action() = default;

    // Constructor for function pointers
    explicit Action(FuncType f);

    // Invoke stored function
    void operator()(TArgs... args) const;

    // Checks if the Action is valid
    explicit operator bool() const;
};

// Include implementation for template functions
#include "action.tpp"

#endif // ACTION_HPP
