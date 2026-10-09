namespace helengine.timeline {
    /// <summary>
    /// Naming rules shared by the validator, the JSON form and hosts that generate timelines: slot, cue and channel names
    /// are lowercase identifiers; timeline ids may also use dots and dashes.
    /// </summary>
    public static class TimelineNames {
        /// <summary>
        /// Longest allowed name or id, in characters.
        /// </summary>
        public const int MaxLength = 64;

        /// <summary>
        /// Checks a slot, cue or channel name: a lowercase letter followed by lowercase letters, digits or underscores.
        /// </summary>
        /// <param name="name">Candidate name.</param>
        /// <returns>True when the name follows the rule.</returns>
        public static bool IsIdentifier(string name) {
            if (string.IsNullOrEmpty(name) || name.Length > MaxLength || !IsLowerLetter(name[0])) {
                return false;
            }
            for (int index = 1; index < name.Length; index++) {
                char character = name[index];
                if (!IsLowerLetter(character) && !IsDigit(character) && character != '_') {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Checks a timeline id or event name: a lowercase letter or digit followed by lowercase letters, digits,
        /// underscores, dots or dashes.
        /// </summary>
        /// <param name="id">Candidate id.</param>
        /// <returns>True when the id follows the rule.</returns>
        public static bool IsId(string id) {
            if (string.IsNullOrEmpty(id) || id.Length > MaxLength || (!IsLowerLetter(id[0]) && !IsDigit(id[0]))) {
                return false;
            }
            for (int index = 1; index < id.Length; index++) {
                char character = id[index];
                if (!IsLowerLetter(character) && !IsDigit(character) && character != '_' && character != '.' && character != '-') {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Checks for an ASCII lowercase letter.
        /// </summary>
        /// <param name="character">Character to test.</param>
        /// <returns>True for a..z.</returns>
        static bool IsLowerLetter(char character) {
            return character >= 'a' && character <= 'z';
        }

        /// <summary>
        /// Checks for an ASCII digit.
        /// </summary>
        /// <param name="character">Character to test.</param>
        /// <returns>True for 0..9.</returns>
        static bool IsDigit(char character) {
            return character >= '0' && character <= '9';
        }
    }
}
