// <copyright file="LexerTests.cs" company="Kjede">
// This file is part of the Kjede project.
// </copyright>

namespace Kjede.Lexer.Logic.Tests;

using Api;

/// <summary>
/// Tests for <see cref="Lexer"/> tokenization.
/// </summary>
public class LexerTests
{
    /// <summary>
    /// The README example exercises space-separated terminals and multiple captures.
    /// </summary>
    [Test]
    public void Tokenize_WhileRule_ProducesExpectedStream()
    {
        var tokens = new Lexer().Tokenize("while = \"while ($cond:expr) {$body:block}\"");

        Assert.That(
            tokens.Select(t => (t.type, t.value)),
            Is.EqualTo(
                new[]
                {
                    (TokenType.Identifier, "while"),
                    (TokenType.Assign, "="),
                    (TokenType.Terminal, "while"),
                    (TokenType.Terminal, "("),
                    (TokenType.Dollar, "$"),
                    (TokenType.Identifier, "cond"),
                    (TokenType.Colon, ":"),
                    (TokenType.Identifier, "expr"),
                    (TokenType.Terminal, ")"),
                    (TokenType.Terminal, "{"),
                    (TokenType.Dollar, "$"),
                    (TokenType.Identifier, "body"),
                    (TokenType.Colon, ":"),
                    (TokenType.Identifier, "block"),
                    (TokenType.Terminal, "}"),
                    (TokenType.EndOfFile, string.Empty),
                }
            )
        );
    }

    /// <summary>
    /// Line and column are tracked against the raw source, including leading indentation, so
    /// downstream tooling can point at the exact character. Exercises the raw-vs-trimmed column
    /// arithmetic in TokenizeLine and the per-character offsets in TokenizePattern.
    /// </summary>
    [Test]
    public void Tokenize_IndentedRule_ReportsRawLineAndColumn()
    {
        var tokens = new Lexer().Tokenize("  y = \"($x:z)\"");

        Assert.That(
            tokens.Select(t => (t.type, t.value, t.line, t.column)),
            Is.EqualTo(
                new[]
                {
                    (TokenType.Identifier, "y", 0, 2),
                    (TokenType.Assign, "=", 0, 4),
                    (TokenType.Terminal, "(", 0, 7),
                    (TokenType.Dollar, "$", 0, 8),
                    (TokenType.Identifier, "x", 0, 9),
                    (TokenType.Colon, ":", 0, 10),
                    (TokenType.Identifier, "z", 0, 11),
                    (TokenType.Terminal, ")", 0, 12),
                    (TokenType.EndOfFile, string.Empty, 1, 0),
                }
            )
        );
    }

    /// <summary>
    /// Multiple rules across lines are numbered per line, and blank / whitespace-only lines are
    /// skipped without emitting tokens.
    /// </summary>
    [Test]
    public void Tokenize_MultiLineWithBlankLines_NumbersLinesAndSkipsBlanks()
    {
        var tokens = new Lexer().Tokenize("a = \"x\"\n   \nb = \"y\"");

        Assert.That(
            tokens.Select(t => (t.type, t.value, t.line)),
            Is.EqualTo(
                new[]
                {
                    (TokenType.Identifier, "a", 0),
                    (TokenType.Assign, "=", 0),
                    (TokenType.Terminal, "x", 0),
                    (TokenType.Identifier, "b", 2),
                    (TokenType.Assign, "=", 2),
                    (TokenType.Terminal, "y", 2),
                    (TokenType.EndOfFile, string.Empty, 3),
                }
            )
        );
    }

    /// <summary>
    /// Carriage-return / line-feed source lexes identically to line-feed source: no stray Unknown
    /// tokens for blank lines and line numbers stay correct, independent of the host platform.
    /// </summary>
    [Test]
    public void Tokenize_CrlfSource_ProducesNoUnknownTokens()
    {
        var tokens = new Lexer().Tokenize("a = \"x\"\r\n\r\nb = \"y\"\r\n");

        Assert.That(tokens.Select(t => t.type), Has.No.Member(TokenType.Unknown));
        Assert.That(
            tokens.Select(t => (t.type, t.value, t.line)),
            Is.EqualTo(
                new[]
                {
                    (TokenType.Identifier, "a", 0),
                    (TokenType.Assign, "=", 0),
                    (TokenType.Terminal, "x", 0),
                    (TokenType.Identifier, "b", 2),
                    (TokenType.Assign, "=", 2),
                    (TokenType.Terminal, "y", 2),
                    (TokenType.EndOfFile, string.Empty, 4),
                }
            )
        );
    }

    /// <summary>
    /// Empty source yields a lone EndOfFile so the parser can uniformly look ahead.
    /// </summary>
    [Test]
    public void Tokenize_EmptySource_ProducesOnlyEndOfFile()
    {
        var tokens = new Lexer().Tokenize(string.Empty);

        Assert.That(
            tokens.Select(t => (t.type, t.value)),
            Is.EqualTo(new[] { (TokenType.EndOfFile, string.Empty) })
        );
    }

    /// <summary>
    /// A line with no '=' separating a rule name from its pattern is surfaced as Unknown rather than
    /// silently dropped.
    /// </summary>
    [Test]
    public void Tokenize_LineWithoutAssign_ProducesUnknown()
    {
        var tokens = new Lexer().Tokenize("not a rule");

        Assert.That(
            tokens.Select(t => (t.type, t.value)),
            Is.EqualTo(
                new[] { (TokenType.Unknown, "not a rule"), (TokenType.EndOfFile, string.Empty) }
            )
        );
    }

    /// <summary>
    /// A pattern that is not wrapped in quotes is Unknown, and a lone quote character is not a valid
    /// quoted pattern.
    /// </summary>
    [Test]
    public void Tokenize_UnquotedPattern_ProducesUnknown()
    {
        var tokens = new Lexer().Tokenize("x = while");

        Assert.That(
            tokens.Select(t => (t.type, t.value)),
            Is.EqualTo(
                new[] { (TokenType.Unknown, "x = while"), (TokenType.EndOfFile, string.Empty) }
            )
        );
    }

    /// <summary>
    /// A missing rule name (line starting with '=') is Unknown rather than an empty-valued
    /// Identifier.
    /// </summary>
    [Test]
    public void Tokenize_MissingRuleName_ProducesUnknown()
    {
        var tokens = new Lexer().Tokenize("= \"x\"");

        Assert.That(
            tokens.Select(t => (t.type, t.value)),
            Is.EqualTo(
                new[] { (TokenType.Unknown, "= \"x\""), (TokenType.EndOfFile, string.Empty) }
            )
        );
    }

    /// <summary>
    /// Pins the documented meta-syntax boundary: a literal '=' inside a pattern is lexed as a second
    /// Assign token (not a Terminal), and inner quotes are skipped like the outer readability quotes.
    /// This is the design in CLAUDE.md — the test exists so any future change to it is deliberate.
    /// </summary>
    [Test]
    public void Tokenize_LiteralAssignInPattern_LexesAsAssign()
    {
        var tokens = new Lexer().Tokenize("assign = \"$x:id = $y:expr\"");

        Assert.That(
            tokens.Select(t => (t.type, t.value)),
            Is.EqualTo(
                new[]
                {
                    (TokenType.Identifier, "assign"),
                    (TokenType.Assign, "="),
                    (TokenType.Dollar, "$"),
                    (TokenType.Identifier, "x"),
                    (TokenType.Colon, ":"),
                    (TokenType.Identifier, "id"),
                    (TokenType.Assign, "="),
                    (TokenType.Dollar, "$"),
                    (TokenType.Identifier, "y"),
                    (TokenType.Colon, ":"),
                    (TokenType.Identifier, "expr"),
                    (TokenType.EndOfFile, string.Empty),
                }
            )
        );
    }

    /// <summary>
    /// A lone carriage return ("old Mac" line ending) separates rules just like a line feed: two
    /// rules are produced on consecutive lines with no stray Unknown tokens. Pins '\r' as a line
    /// separator in the split.
    /// </summary>
    [Test]
    public void Tokenize_BareCarriageReturnSource_SplitsLinesLikeLineFeed()
    {
        var tokens = new Lexer().Tokenize("a = \"x\"\rb = \"y\"");

        Assert.That(tokens.Select(t => t.type), Has.No.Member(TokenType.Unknown));
        Assert.That(
            tokens.Select(t => (t.type, t.value, t.line)),
            Is.EqualTo(
                new[]
                {
                    (TokenType.Identifier, "a", 0),
                    (TokenType.Assign, "=", 0),
                    (TokenType.Terminal, "x", 0),
                    (TokenType.Identifier, "b", 1),
                    (TokenType.Assign, "=", 1),
                    (TokenType.Terminal, "y", 1),
                    (TokenType.EndOfFile, string.Empty, 2),
                }
            )
        );
    }

    /// <summary>
    /// A literal '=' inside a pattern reports its column relative to the line start, not the pattern
    /// start. Pins the <c>column + index</c> arithmetic in ScanSegment's Assign case.
    /// </summary>
    [Test]
    public void Tokenize_LiteralAssignInPattern_ReportsColumnFromLineStart()
    {
        var tokens = new Lexer().Tokenize("x = \"a = b\"");

        Assert.That(
            tokens.Select(t => (t.type, t.value, t.line, t.column)),
            Is.EqualTo(
                new[]
                {
                    (TokenType.Identifier, "x", 0, 0),
                    (TokenType.Assign, "=", 0, 2),
                    (TokenType.Terminal, "a", 0, 5),
                    (TokenType.Assign, "=", 0, 7),
                    (TokenType.Terminal, "b", 0, 9),
                    (TokenType.EndOfFile, string.Empty, 1, 0),
                }
            )
        );
    }

    /// <summary>
    /// A meta marker ('$' or ':') with no following name emits no empty-valued Identifier: the
    /// name-capture is simply absent. Pins the <c>end &gt; start</c> guard in ScanMarkerWithIdentifier.
    /// </summary>
    [Test]
    public void Tokenize_MetaMarkerWithNoName_EmitsNoEmptyIdentifier()
    {
        var tokens = new Lexer().Tokenize("x = \"$:c\"");

        Assert.That(
            tokens.Select(t => (t.type, t.value)),
            Is.EqualTo(
                new[]
                {
                    (TokenType.Identifier, "x"),
                    (TokenType.Assign, "="),
                    (TokenType.Dollar, "$"),
                    (TokenType.Colon, ":"),
                    (TokenType.Identifier, "c"),
                    (TokenType.EndOfFile, string.Empty),
                }
            )
        );
    }

    /// <summary>
    /// An empty quoted pattern (exactly two characters) is a valid, if empty, pattern: it yields the
    /// identifier and assign with no pattern tokens. Pins the <c>Length &lt; 2</c> lower bound in
    /// QuotedBody.
    /// </summary>
    [Test]
    public void Tokenize_EmptyQuotedPattern_IsAcceptedWithNoPatternTokens()
    {
        var tokens = new Lexer().Tokenize("x = \"\"");

        Assert.That(
            tokens.Select(t => (t.type, t.value)),
            Is.EqualTo(
                new[]
                {
                    (TokenType.Identifier, "x"),
                    (TokenType.Assign, "="),
                    (TokenType.EndOfFile, string.Empty),
                }
            )
        );
    }

    /// <summary>
    /// A lone quote is not a quoted pattern: a pattern must be at least two characters and start with
    /// a quote. Pins the opening-quote requirement in QuotedBody.
    /// </summary>
    [Test]
    public void Tokenize_LoneQuotePattern_ProducesUnknown()
    {
        var tokens = new Lexer().Tokenize("x = \"");

        Assert.That(
            tokens.Select(t => (t.type, t.value)),
            Is.EqualTo(new[] { (TokenType.Unknown, "x = \""), (TokenType.EndOfFile, string.Empty) })
        );
    }

    /// <summary>
    /// A pattern that ends with a quote but does not start with one is Unknown: both delimiters are
    /// required. Pins the opening-quote requirement in QuotedBody.
    /// </summary>
    [Test]
    public void Tokenize_PatternWithClosingQuoteOnly_ProducesUnknown()
    {
        var tokens = new Lexer().Tokenize("x = a\"");

        Assert.That(
            tokens.Select(t => (t.type, t.value)),
            Is.EqualTo(
                new[] { (TokenType.Unknown, "x = a\""), (TokenType.EndOfFile, string.Empty) }
            )
        );
    }

    /// <summary>
    /// A rule name is held to the same character set as an in-pattern identifier: a name containing
    /// whitespace is not a valid rule name and the whole line is surfaced as Unknown rather than an
    /// Identifier with an embedded space. Pins IsValidRuleName against split[0].
    /// </summary>
    [Test]
    public void Tokenize_RuleNameWithSpace_ProducesUnknown()
    {
        var tokens = new Lexer().Tokenize("foo bar = \"x\"");

        Assert.That(
            tokens.Select(t => (t.type, t.value)),
            Is.EqualTo(
                new[]
                {
                    (TokenType.Unknown, "foo bar = \"x\""),
                    (TokenType.EndOfFile, string.Empty),
                }
            )
        );
    }

    /// <summary>
    /// Each reserved meta character can be written verbatim in a pattern by escaping it with a
    /// backslash, producing a single-character Terminal with the backslash stripped from the value.
    /// Pins the escape branch in ScanTerminalRun and the Escapable set.
    /// </summary>
    /// <param name="source">The Kjede source line whose pattern holds a single escaped character.</param>
    /// <param name="terminal">The expected resolved value of the produced Terminal token.</param>
    [TestCase("x = \"\\$\"", "$")]
    [TestCase("x = \"\\:\"", ":")]
    [TestCase("x = \"\\=\"", "=")]
    [TestCase("x = \"\\\"\"", "\"")]
    [TestCase("x = \"\\\\\"", "\\")]
    public void Tokenize_EscapedReservedChar_ProducesLiteralTerminal(string source, string terminal)
    {
        var tokens = new Lexer().Tokenize(source);

        Assert.That(
            tokens.Select(t => (t.type, t.value)),
            Is.EqualTo(
                new[]
                {
                    (TokenType.Identifier, "x"),
                    (TokenType.Assign, "="),
                    (TokenType.Terminal, terminal),
                    (TokenType.EndOfFile, string.Empty),
                }
            )
        );
    }

    /// <summary>
    /// An escaped reserved character joins the surrounding run into a single Terminal rather than
    /// splitting it: <c>a\$b</c> lexes as the one terminal <c>a$b</c>. Pins run continuation across
    /// an escape in ScanTerminalRun.
    /// </summary>
    [Test]
    public void Tokenize_EscapedCharInsideRun_MergesIntoOneTerminal()
    {
        var tokens = new Lexer().Tokenize("x = \"a\\$b\"");

        Assert.That(
            tokens.Select(t => (t.type, t.value)),
            Is.EqualTo(
                new[]
                {
                    (TokenType.Identifier, "x"),
                    (TokenType.Assign, "="),
                    (TokenType.Terminal, "a$b"),
                    (TokenType.EndOfFile, string.Empty),
                }
            )
        );
    }

    /// <summary>
    /// An escaped quote inside a pattern is a literal terminal character and does not close the
    /// pattern: the real closing quote is the first *unescaped* one. Pins escape-awareness in
    /// QuotedBody.
    /// </summary>
    [Test]
    public void Tokenize_EscapedQuoteInPattern_DoesNotCloseThePattern()
    {
        var tokens = new Lexer().Tokenize("x = \"a\\\"b\"");

        Assert.That(
            tokens.Select(t => (t.type, t.value)),
            Is.EqualTo(
                new[]
                {
                    (TokenType.Identifier, "x"),
                    (TokenType.Assign, "="),
                    (TokenType.Terminal, "a\"b"),
                    (TokenType.EndOfFile, string.Empty),
                }
            )
        );
    }

    /// <summary>
    /// A backslash escaping a non-escapable character, and a dangling backslash before the closing
    /// quote, are both malformed and surface the whole pattern as Unknown. Pins escape validation
    /// in QuotedBody.
    /// </summary>
    /// <param name="source">The Kjede source line whose pattern contains a malformed escape.</param>
    /// <param name="unknownValue">The expected value of the single Unknown token (the raw pattern).</param>
    [TestCase("x = \"\\a\"", "x = \"\\a\"")]
    [TestCase("x = \"a\\\\\\\"", "x = \"a\\\\\\\"")]
    public void Tokenize_InvalidEscape_ProducesUnknown(string source, string unknownValue)
    {
        var tokens = new Lexer().Tokenize(source);

        Assert.That(
            tokens.Select(t => (t.type, t.value)),
            Is.EqualTo(
                new[] { (TokenType.Unknown, unknownValue), (TokenType.EndOfFile, string.Empty) }
            )
        );
    }

    /// <summary>
    /// Two separately quoted runs on one line no longer silently concatenate: the closing quote is
    /// the first unescaped one, so trailing content past it makes the pattern Unknown. Pins the
    /// exact-boundary requirement (<c>close == Length - 1</c>) in TokenizeAssignment.
    /// </summary>
    [Test]
    public void Tokenize_TwoQuotedRunsOnOneLine_ProducesUnknown()
    {
        var tokens = new Lexer().Tokenize("x = \"a\"  \"b\"");

        Assert.That(
            tokens.Select(t => (t.type, t.value)),
            Is.EqualTo(
                new[]
                {
                    (TokenType.Unknown, "x = \"a\"  \"b\""),
                    (TokenType.EndOfFile, string.Empty),
                }
            )
        );
    }

    /// <summary>
    /// A line that is a single valid identifier with no '=' is Unknown, not a rule: the no-assign
    /// guard in TokenizeLine must fire before the rule-name check reaches a non-existent pattern.
    /// </summary>
    [Test]
    public void Tokenize_BareIdentifierLine_ProducesUnknown()
    {
        var tokens = new Lexer().Tokenize("while");

        Assert.That(
            tokens.Select(t => (t.type, t.value)),
            Is.EqualTo(new[] { (TokenType.Unknown, "while"), (TokenType.EndOfFile, string.Empty) })
        );
    }

    /// <summary>
    /// A pattern opened with a quote but never closed is Unknown, even at the two-character minimum
    /// (<c>"a</c>): with no unescaped closing quote, QuotedBody yields no valid boundary. Pins the
    /// "not found" return in QuotedBody against being mistaken for a boundary.
    /// </summary>
    [Test]
    public void Tokenize_UnterminatedTwoCharPattern_ProducesUnknown()
    {
        var tokens = new Lexer().Tokenize("x = \"a");

        Assert.That(
            tokens.Select(t => (t.type, t.value)),
            Is.EqualTo(
                new[] { (TokenType.Unknown, "x = \"a"), (TokenType.EndOfFile, string.Empty) }
            )
        );
    }

    /// <summary>
    /// An unterminated pattern whose last characters are an escape sequence is Unknown: QuotedBody
    /// must consume the escape and run off the end without dereferencing past it. Pins escape-aware
    /// scanning against an out-of-bounds read at the boundary.
    /// </summary>
    [Test]
    public void Tokenize_UnterminatedPatternEndingInEscape_ProducesUnknown()
    {
        var tokens = new Lexer().Tokenize("x = \"a\\b");

        Assert.That(
            tokens.Select(t => (t.type, t.value)),
            Is.EqualTo(
                new[] { (TokenType.Unknown, "x = \"a\\b"), (TokenType.EndOfFile, string.Empty) }
            )
        );
    }

    /// <summary>
    /// A pattern whose final character is a bare backslash is Unknown: the escape dangles with
    /// nothing to escape. Pins the dangling-'\'-at-end guard in QuotedBody (<c>index + 1 &lt;
    /// pattern.Length</c>) — the boundary that operating on the raw pattern, rather than a
    /// pre-validated body, makes reachable and testable.
    /// </summary>
    [Test]
    public void Tokenize_PatternEndingInBareBackslash_ProducesUnknown()
    {
        var tokens = new Lexer().Tokenize("x = \"a\\");

        Assert.That(
            tokens.Select(t => (t.type, t.value)),
            Is.EqualTo(
                new[] { (TokenType.Unknown, "x = \"a\\"), (TokenType.EndOfFile, string.Empty) }
            )
        );
    }
}
