#pragma once

#include <stdexcept>
#include <string>

class Exception : public std::runtime_error {
public:
    Exception()
        : std::runtime_error("Exception") {
    }

    explicit Exception(const std::string& message)
        : std::runtime_error(message) {
    }
};

class ArgumentException : public Exception {
public:
    ArgumentException()
        : Exception("Invalid argument.") {
    }

    explicit ArgumentException(const std::string& message)
        : Exception(message) {
    }

    ArgumentException(const std::string& message, const std::string& parameterName)
        : Exception(message + " Parameter name: " + parameterName) {
    }
};

class ArgumentNullException : public ArgumentException {
public:
    ArgumentNullException()
        : ArgumentException("Value cannot be null.") {
    }

    explicit ArgumentNullException(const std::string& parameterName)
        : ArgumentException("Value cannot be null. Parameter name: " + parameterName) {
    }
};

class ArgumentOutOfRangeException : public ArgumentException {
public:
    ArgumentOutOfRangeException()
        : ArgumentException("Specified argument was out of range.") {
    }

    explicit ArgumentOutOfRangeException(const std::string& parameterName)
        : ArgumentException("Specified argument was out of range. Parameter name: " + parameterName) {
    }

    ArgumentOutOfRangeException(const std::string& parameterName, const std::string& message)
        : ArgumentException(message + " Parameter name: " + parameterName) {
    }
};

class InvalidOperationException : public Exception {
public:
    InvalidOperationException()
        : Exception("Operation is not valid due to the current state of the object.") {
    }

    explicit InvalidOperationException(const std::string& message)
        : Exception(message) {
    }
};

class EndOfStreamException : public Exception {
public:
    EndOfStreamException()
        : Exception("Unable to read beyond the end of the stream.") {
    }

    explicit EndOfStreamException(const std::string& message)
        : Exception(message) {
    }
};

class FileNotFoundException : public Exception {
public:
    FileNotFoundException()
        : Exception("Unable to find the specified file.") {
    }

    explicit FileNotFoundException(const std::string& message)
        : Exception(message) {
    }

    FileNotFoundException(const std::string& message, const std::string& fileName)
        : Exception(message + " File name: " + fileName) {
    }
};

class DirectoryNotFoundException : public Exception {
public:
    DirectoryNotFoundException()
        : Exception("Unable to find the specified directory.") {
    }

    explicit DirectoryNotFoundException(const std::string& message)
        : Exception(message) {
    }
};

class NotSupportedException : public Exception {
public:
    NotSupportedException()
        : Exception("Specified method is not supported.") {
    }

    explicit NotSupportedException(const std::string& message)
        : Exception(message) {
    }
};
