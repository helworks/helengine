#include <fcntl.h>
#include <stdint.h>
#include <sys/types.h>

/* Fixed-arity entrypoints keep Darwin's variadic ABI inside the C compiler.
 * In particular, Apple arm64 passes open/openat modes and fcntl operands on
 * the stack. Managed callers must not guess that calling convention. */

/// Opens a path with native flags and an explicit creation mode while preserving errno.
__attribute__((visibility("default")))
int helengine_authoring_open(const char* path, int flags, uint32_t mode) {
    return open(path, flags, (mode_t)mode);
}

/// Opens a leaf relative to its pinned parent descriptor without following a replacement path.
__attribute__((visibility("default")))
int helengine_authoring_openat(int directory_fd, const char* path, int flags, uint32_t mode) {
    return openat(directory_fd, path, flags, (mode_t)mode);
}

/// Passes the integer fcntl operand through the compiler's native variadic ABI without altering errno.
__attribute__((visibility("default")))
int helengine_authoring_fcntl(int file_descriptor, int command, int argument) {
    return fcntl(file_descriptor, command, argument);
}
