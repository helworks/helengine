#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

#include "runtime/native_string.hpp"

class TextBoxEditState
{
public:
    virtual ~TextBoxEditState() = default;

    int32_t get_CursorPosition();

    void set_CursorPosition(int32_t value);

    bool get_HasSelection();

    int32_t get_SelectionEnd();

    int32_t get_SelectionStart();

    std::string get_Text();

    void set_Text(std::string value);

    void Backspace();

    void ClearSelection();

    void Delete();

    void InsertCharacter(char character);

    void MoveCursorLeft();

    void MoveCursorRight();

    TextBoxEditState();

    TextBoxEditState(std::string text);

    void SelectAll();

    void SetCursorToEnd();

    void SetCursorToStart();

    void SetSelection(int32_t anchorPosition, int32_t cursorPosition);
private:
    int32_t CursorPositionValue;

    int32_t SelectionAnchorPositionValue;

    std::string TextValue;

    int32_t ClampCursor(int32_t value);

    bool RemoveSelection();
};
