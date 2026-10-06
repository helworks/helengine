using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace helengine.vfx.captions;

/// <summary>Imports SubRip and Whisper JSON without fabricating word timestamps.</summary>
public static class CaptionImporter {
    /// <summary>Reads a UTF-8 SRT or JSON transcription according to its extension.</summary>
    public static CaptionDocument Read(string path) {
        return Path.GetExtension(path).ToLowerInvariant() switch {
            ".srt" => ParseSrt(File.ReadAllText(path)),
            ".json" => ParseWhisperJson(File.ReadAllText(path)),
            _ => throw new FormatException("Choose an .srt or Whisper .json transcription.")
        };
    }

    /// <summary>Parses multiline SubRip cues, optional numbering, BOM and CRLF line endings.</summary>
    public static CaptionDocument ParseSrt(string source) {
        ArgumentNullException.ThrowIfNull(source);
        string normalized = source.TrimStart('\uFEFF').Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        var cues = new List<CaptionCue>();
        foreach (string block in Regex.Split(normalized, @"\n[ \t]*\n+")) {
            string[] lines = block.Split('\n');
            int timingIndex = lines.Length > 0 && lines[0].Contains("-->", StringComparison.Ordinal) ? 0 : 1;
            if (lines.Length <= timingIndex + 1) {
                throw new FormatException($"SRT cue {cues.Count + 1} is missing its timing or text.");
            }
            if (timingIndex == 1 && !int.TryParse(lines[0].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out _)) {
                throw new FormatException($"SRT cue {cues.Count + 1} has an invalid index.");
            }
            Match match = Regex.Match(lines[timingIndex], @"^\s*(\d+:\d{2}:\d{2}[,.]\d{3})\s*-->\s*(\d+:\d{2}:\d{2}[,.]\d{3})\s*$");
            if (!match.Success) {
                throw new FormatException($"SRT cue {cues.Count + 1} has an invalid timestamp line.");
            }
            string text = string.Join("\n", lines.Skip(timingIndex + 1));
            text = Regex.Replace(text, @"</?(?:b|i|u|font)(?:\s+[^>]*)?>", "", RegexOptions.IgnoreCase);
            cues.Add(new CaptionCue(text, ParseTimestamp(match.Groups[1].Value), ParseTimestamp(match.Groups[2].Value)));
        }
        return new CaptionDocument(cues);
    }

    /// <summary>Reads standard Whisper segments, root word timestamps, or an array of segments.</summary>
    public static CaptionDocument ParseWhisperJson(string source) {
        using JsonDocument json = JsonDocument.Parse(source);
        JsonElement root = json.RootElement;
        var cues = new List<CaptionCue>();
        CaptionWord[] rootWords = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("words", out JsonElement words)
            ? ReadWords(words) : Array.Empty<CaptionWord>();
        bool hasNestedWords = false;
        JsonElement segments;
        if (root.ValueKind == JsonValueKind.Array) {
            segments = root;
        } else if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("segments", out segments)) {
            if (segments.ValueKind != JsonValueKind.Array) {
                throw new FormatException("Whisper segments must be an array.");
            }
        } else if (rootWords.Length > 0) {
            cues.Add(new CaptionCue(string.Join(" ", rootWords.Select(word => word.Text)), rootWords[0].Start, rootWords.Max(word => word.End), rootWords));
            return new CaptionDocument(cues);
        } else {
            throw new FormatException("Whisper JSON needs segments or words with start/end timestamps.");
        }
        foreach (JsonElement segment in segments.EnumerateArray()) {
            double start = ReadTime(segment, "start");
            double end = ReadTime(segment, "end");
            string text = ReadText(segment, "text");
            CaptionWord[] aligned;
            if (segment.TryGetProperty("words", out JsonElement nestedWords)) {
                hasNestedWords = true;
                aligned = ReadWords(nestedWords);
            } else {
                aligned = rootWords.Where(word => word.Start >= start && word.End <= end).ToArray();
            }
            cues.Add(new CaptionCue(text, start, end, aligned));
        }
        if (!hasNestedWords && rootWords.Any(word => !cues.Any(cue => cue.Words.Contains(word)))) {
            throw new FormatException("Some root word timestamps fall outside the supplied segments. Use matching segments/words or a words-only JSON file.");
        }
        return new CaptionDocument(cues);
    }

    /// <summary>Validates hours, minutes, seconds and millisecond components of a SubRip time.</summary>
    static double ParseTimestamp(string timestamp) {
        string[] parts = timestamp.Replace(',', '.').Split(':', '.');
        double hours = double.Parse(parts[0], CultureInfo.InvariantCulture);
        int minutes = int.Parse(parts[1], CultureInfo.InvariantCulture);
        int seconds = int.Parse(parts[2], CultureInfo.InvariantCulture);
        int milliseconds = int.Parse(parts[3], CultureInfo.InvariantCulture);
        if (minutes > 59 || seconds > 59) {
            throw new FormatException($"Invalid SRT timestamp: {timestamp}.");
        }
        return hours * 3600 + minutes * 60 + seconds + milliseconds / 1000.0;
    }

    /// <summary>Reads an explicit numeric time rather than supplying a default for missing data.</summary>
    static double ReadTime(JsonElement element, string name) {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out double time)) {
            throw new FormatException($"Whisper entry is missing numeric '{name}'.");
        }
        return time;
    }

    /// <summary>Reads one required text property with a descriptive import failure.</summary>
    static string ReadText(JsonElement element, string name) {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.String) {
            throw new FormatException($"Whisper entry is missing text '{name}'.");
        }
        return value.GetString();
    }

    /// <summary>Preserves Whisper's 'word' field and the 'text' spelling used by some parsers.</summary>
    static CaptionWord[] ReadWords(JsonElement array) {
        if (array.ValueKind != JsonValueKind.Array) {
            throw new FormatException("Whisper words must be an array.");
        }
        var result = new List<CaptionWord>();
        foreach (JsonElement word in array.EnumerateArray()) {
            string property = word.ValueKind == JsonValueKind.Object && word.TryGetProperty("word", out _) ? "word" : "text";
            result.Add(new CaptionWord(ReadText(word, property), ReadTime(word, "start"), ReadTime(word, "end")));
        }
        return result.ToArray();
    }
}
