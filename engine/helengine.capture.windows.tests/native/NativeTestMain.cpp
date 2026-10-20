#include <exception>
#include <iostream>
/// <summary>Executes the GPU resource contract group.</summary>
void RunFrameResourceTests();
/// <summary>Executes deterministic session and error contracts.</summary>
void RunSessionContractTests();
/// <summary>Returns a failing process status for any native assertion or unhandled native error.</summary>
int main() {
    try { RunFrameResourceTests(); RunSessionContractTests(); return 0; }
    catch (const std::exception& error) { std::cerr << "FAIL " << error.what() << '\n'; return 1; }
}
