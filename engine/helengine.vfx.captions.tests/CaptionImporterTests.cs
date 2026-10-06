namespace helengine.vfx.captions.tests;

/// <summary>Checks real transcription boundaries, supported shapes and descriptive malformed-input failures.</summary>
public sealed class CaptionImporterTests {
    /// <summary>SubRip BOM, CRLF, line breaks and noncontiguous numbering retain phrase-only times.</summary>
    [Fact]
    public void SubRipPreservesTextAndExclusiveBoundaries() {
        CaptionDocument document = CaptionImporter.ParseSrt("\uFEFF4\r\n00:00:01,250 --> 00:00:02,500\r\n<b>Olá</b> mundo!\r\nSegunda linha\r\n\r\n9\r\n00:00:03,000 --> 00:00:04,000\r\nTchau\r\n");
        Assert.Equal(2, document.Cues.Count);
        Assert.Equal("Olá mundo!\nSegunda linha", document.Cues[0].Text);
        Assert.Empty(document.Cues[0].Words);
        Assert.Null(document.FindCue(1.249));
        Assert.Same(document.Cues[0], document.FindCue(1.25));
        Assert.Null(document.FindCue(2.5));
        Assert.Equal(4, document.Duration);
    }

    /// <summary>Standard Whisper segment-level word alignment is retained without redistributing times.</summary>
    [Fact]
    public void WhisperNestedWordsKeepOriginalAlignment() {
        CaptionDocument document = CaptionImporter.ParseWhisperJson("""
            {"segments":[{"start":1,"end":3,"text":" Olá mundo!","words":[
              {"start":1.2,"end":1.7,"word":" Olá"},{"start":2.1,"end":2.9,"word":" mundo!"}]}]}
            """);
        Assert.Equal("Olá", document.Cues[0].Words[0].Text);
        Assert.Equal(1.2, document.Cues[0].Words[0].Start);
        Assert.Equal(2.1, document.Cues[0].Words[1].Start);
        Assert.Equal(2.9, document.Cues[0].Words[1].End);
    }

    /// <summary>Verbose API JSON can associate root words with segments, or contain only word timestamps.</summary>
    [Fact]
    public void WhisperRootWordsAndSegmentArraysAreSupported() {
        CaptionDocument aligned = CaptionImporter.ParseWhisperJson("""
            {"segments":[{"start":0,"end":2,"text":"hello world"}],
             "words":[{"start":0.1,"end":0.5,"word":"hello"},{"start":0.7,"end":1.8,"word":"world"}]}
            """);
        Assert.Equal(2, aligned.Cues[0].Words.Count);
        CaptionDocument wordsOnly = CaptionImporter.ParseWhisperJson("""
            {"words":[{"start":0.1,"end":0.5,"text":"hello"},{"start":0.7,"end":1.8,"text":"world"}]}
            """);
        Assert.Equal("hello world", wordsOnly.Cues[0].Text);
        Assert.Equal(1.8, wordsOnly.Duration);
        Assert.Empty(CaptionImporter.ParseWhisperJson("""[{"start":0,"end":1,"text":"phrase"}]""").Cues[0].Words);
    }

    /// <summary>Whisper can emit zero-length punctuation without requiring estimated durations.</summary>
    [Fact]
    public void ZeroDurationWordsArePreserved() {
        CaptionDocument document = CaptionImporter.ParseWhisperJson("""
            {"segments":[{"start":0,"end":1,"text":"hello !","words":[
              {"word":"hello","start":0,"end":0.5},{"word":"!","start":0.5,"end":0.5}]}]}
            """);
        Assert.Equal(2, document.Cues[0].Words.Count);
        Assert.Equal(document.Cues[0].Words[1].Start, document.Cues[0].Words[1].End);
    }

    /// <summary>Invalid timing, absent text, unknown JSON shape and out-of-order words fail at import.</summary>
    [Theory]
    [InlineData("{\"text\":\"no timing\"}")]
    [InlineData("{\"segments\":[{\"start\":0,\"text\":\"missing end\"}]}")]
    [InlineData("{\"segments\":[{\"start\":\"0\",\"end\":1,\"text\":\"wrong type\"}]}")]
    [InlineData("{\"segments\":[{\"start\":2,\"end\":1,\"text\":\"reversed\"}]}")]
    [InlineData("{\"segments\":[{\"start\":0,\"end\":1,\"text\":\"word outside\",\"words\":[{\"word\":\"bad\",\"start\":0.5,\"end\":2}]}]}")]
    [InlineData("{\"segments\":[{\"start\":0,\"end\":1,\"text\":\"word outside\"}],\"words\":[{\"word\":\"bad\",\"start\":0.5,\"end\":2}]}")]
    public void MalformedWhisperIsRejected(string json) => Assert.Throws<FormatException>(() => CaptionImporter.ParseWhisperJson(json));

    /// <summary>SubRip timing errors do not become zero-duration or untimed captions.</summary>
    [Theory]
    [InlineData("1\n00:61:00,000 --> 00:62:00,000\nBad")]
    [InlineData("1\n00:00:02,000 --> 00:00:01,000\nBad")]
    [InlineData("1\n00:00:00,000 --> 00:00:01,000")]
    [InlineData("")]
    public void MalformedSubRipIsRejected(string source) => Assert.Throws<FormatException>(() => CaptionImporter.ParseSrt(source));

    /// <summary>Overlapping cues prefer the most recent start, then reveal the remaining older cue.</summary>
    [Fact]
    public void OverlappingCuesResolveAtActualEndTimes() {
        var older = new CaptionCue("older", 0, 10);
        var newer = new CaptionCue("newer", 2, 3);
        var document = new CaptionDocument(new[] { newer, older });
        Assert.Same(newer, document.FindCue(2.5));
        Assert.Same(older, document.FindCue(3));
        Assert.Null(document.FindCue(10));
    }
}
