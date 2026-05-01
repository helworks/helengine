#include "console.hpp"
#include "io/file.hpp"
#include <iostream>

using namespace std;

bool Console::WriteLine(char* fileName) 
{
	cout << fileName;
    return true;
}
