// <copyright file="Lexer.cs" company="Kjede">
// This file is part of the Kjede project.
// </copyright>

namespace Kjede.Lexer.Logic;

using System.Collections.Immutable;
using System.Text;
using Api;

/// <inheritdoc />
public sealed class Lexer : ILexer
{
    /// <summary>
    /// Characters that carry meaning in Kjede's meta-syntax and so cannot appear verbatim in a
    /// pattern. Prefixing one with <c>\</c> escapes it into a literal <see cref="TokenType.Terminal"/>
    /// character; <c>\</c> itself is escapable so a literal backslash can be written as <c>\\</c>.
    /// </summary>
    private const string Escapable = "$:=\"\\";

    /// <inheritdoc />
    public ImmutableArray<Token> Tokenize(string source)
    {
        // Empty source is special-cased so its lone EndOfFile lands on line 0: "".Split(...) yields
        // a single empty line, which would otherwise push the EndOfFile to line 1.
        if (string.IsNullOrEmpty(source))
        {
            return [new Token(TokenType.EndOfFile, string.Empty, 0, 0)];
        }

        var tokens = ImmutableArray.CreateBuilder<Token>();
        var lines = source.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);
        var lineIndex = 0;

        foreach (var line in lines)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                tokens.AddRange(TokenizeLine(line, lineIndex));
            }

            lineIndex += 1;
        }

        tokens.Add(new Token(TokenType.EndOfFile, string.Empty, lineIndex, 0));
        return tokens.ToImmutable();
    }

    private static ImmutableArray<Token> TokenizeLine(string line, int lineNumber)
    {
        var split = line.Split('=', 2);

        if (split.Length != 2)
        {
            return Unknown(line, lineNumber, 0);
        }

        var identifier = split[0].Trim();

        return IsValidRuleName(identifier)
            ? TokenizeAssignment(split, identifier, lineNumber, line)
            : Unknown(line, lineNumber, 0);
    }

    private static ImmutableArray<Token> TokenizeAssignment(
        string[] split,
        string identifier,
        int lineNumber,
        string line
    )
    {
        var pattern = split[1].Trim();
        var identifierColumn = LeadingWhitespace(split[0]);
        var assignColumn = split[0].Length;
        var patternColumn = assignColumn + 1 + LeadingWhitespace(split[1]); // +1 for the consumed '='

        // A pattern is a quoted body whose closing quote is the first *unescaped* '"'. The body
        // retains every raw character (backslashes included), so column = start + index stays exact.
        var body = QuotedBody(pattern);

        if (body is null)
        {
            // A malformed pattern makes the whole line unlexable; surface the entire raw line as a
            // single Unknown at column 0, matching the line-level failures in TokenizeLine so every
            // Unknown token carries the same recoverable unit.
            return Unknown(line, lineNumber, 0);
        }

        return
        [
            new Token(TokenType.Identifier, identifier, lineNumber, identifierColumn),
            new Token(TokenType.Assign, "=", lineNumber, assignColumn),
            .. TokenizePattern(body, lineNumber, patternColumn + 1), // +1 for the open quote
        ];
    }

    private static ImmutableArray<Token> TokenizePattern(string pattern, int line, int column)
    {
        var tokens = ImmutableArray.CreateBuilder<Token>();
        var index = 0;

        while (index < pattern.Length)
        {
            var (next, produced) = ScanSegment(pattern, line, column, index);
            tokens.AddRange(produced);
            index = next;
        }

        return tokens.ToImmutable();
    }

    private static (int next, ImmutableArray<Token> tokens) ScanSegment(
        string pattern,
        int line,
        int column,
        int index
    )
    {
        var c = pattern[index];

        if (char.IsWhiteSpace(c))
        {
            return (index + 1, []); // whitespace is a readability delimiter, not a token
        }

        return c switch
        {
            '$' => ScanMarkerWithIdentifier(TokenType.Dollar, pattern, line, column, index),
            ':' => ScanMarkerWithIdentifier(TokenType.Colon, pattern, line, column, index),
            '=' => (index + 1, [new Token(TokenType.Assign, "=", line, column + index)]),
            _ => ScanTerminalRun(pattern, line, column, index),
        };
    }

    private static (int next, ImmutableArray<Token> tokens) ScanMarkerWithIdentifier(
        TokenType type,
        string pattern,
        int line,
        int column,
        int index
    )
    {
        var marker = new Token(type, pattern[index].ToString(), line, column + index);
        var start = index + 1;
        var end = start;

        // An identifier is captured only directly after a marker.
        while (end < pattern.Length && IsIdentifierChar(pattern[end]))
        {
            end += 1;
        }

        return end > start
            ? (
                end,
                [marker, new Token(TokenType.Identifier, pattern[start..end], line, column + start)]
            )
            : (end, [marker]);
    }

    private static (int next, ImmutableArray<Token> tokens) ScanTerminalRun(
        string pattern,
        int line,
        int column,
        int index
    )
    {
        var value = new StringBuilder();
        var end = index;

        while (end < pattern.Length && IsTerminalChar(pattern[end]))
        {
            // A '\' resolves the next char into a literal. QuotedBody has already rejected any
            // dangling or non-escapable '\' in this body, so pattern[end + 1] is guaranteed present.
            value.Append(pattern[end] == '\\' ? pattern[++end] : pattern[end]);
            end += 1;
        }

        return (end, [new Token(TokenType.Terminal, value.ToString(), line, column + index)]);
    }

    /// <summary>
    /// The raw body between the quotes of a well-formed pattern, or <c>null</c> if <paramref name="pattern"/>
    /// is not a single quote-delimited body that closes exactly at its end with only well-formed escapes
    /// (every <c>\</c> escaping an <see cref="Escapable"/> character, none dangling). The returned body
    /// retains its raw backslashes, so column = start + index stays exact downstream.
    /// </summary>
    private static string? QuotedBody(string pattern)
    {
        if (pattern.Length < 2 || pattern[0] != '"')
        {
            return null;
        }

        var index = 1;

        while (index < pattern.Length)
        {
            switch (pattern[index])
            {
                case '"':
                    // The close must be the last char: trailing content past it is malformed.
                    return index == pattern.Length - 1 ? pattern[1..index] : null;
                case '\\' when index + 1 < pattern.Length && Escapable.Contains(pattern[index + 1]):
                    index += 2; // valid escape: skip the '\' and the escaped character
                    break;
                case '\\':
                    return null; // dangling '\' at end, or '\' before a non-escapable character
                default:
                    index += 1;
                    break;
            }
        }

        return null; // no unescaped closing quote
    }

    private static int LeadingWhitespace(string s) => s.Length - s.TrimStart().Length;

    private static bool IsValidRuleName(string s) => s.Length > 0 && s.All(IsIdentifierChar);

    private static bool IsIdentifierChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    // '\' is a run member so it can introduce an escape; the reserved meta chars ($ : = ") and
    // whitespace end a terminal run (an escaped reserved char reaches here as '\' + the char).
    private static bool IsTerminalChar(char c) =>
        c is not ('$' or ':' or '=' or '"') && !char.IsWhiteSpace(c);

    private static ImmutableArray<Token> Unknown(string value, int line, int column) =>
        [new Token(TokenType.Unknown, value, line, column)];
}
