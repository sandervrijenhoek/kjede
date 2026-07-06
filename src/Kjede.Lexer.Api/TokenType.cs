// <copyright file="TokenType.cs" company="Kjede">
// This file is part of the Kjede project.
// </copyright>

namespace Kjede.Lexer.Api;

/// <summary>
/// The kind of a lexed <see cref="Token"/> in a Kjede rule definition — either a piece of Kjede's
/// own meta-syntax (<see cref="Assign"/>, <see cref="Dollar"/>, <see cref="Colon"/>) or a chunk of
/// the target language being defined ( <see cref="Terminal"/>).
/// </summary>
public enum TokenType
{
    /// <summary>
    /// The <c>=</c> separating a rule name from its pattern, e.g. the <c>=</c> in
    /// <c>while = "while ($cond:expr) {$body:block}"</c>.
    /// </summary>
    Assign,

    /// <summary>
    /// A rule name (e.g. <c>while</c>, <c>Expr</c>), or, following a <see cref="Dollar"/>, either
    /// half of a metavariable capture: the AST field name (e.g. <c>cond</c>) or its fragment
    /// specifier (e.g. <c>expr</c>). In both positions an identifier is one or more letters, digits,
    /// or underscores; a rule name that is not (e.g. one containing a space) is lexed as
    /// <see cref="Unknown"/> rather than an <see cref="Identifier"/>.
    /// </summary>
    Identifier,

    /// <summary>
    /// The <c>$</c> introducing a metavariable capture, e.g. the <c>$</c> in <c>$cond:expr</c>.
    /// </summary>
    Dollar,

    /// <summary>
    /// The <c>:</c> inside a metavariable capture, separating the AST field name from its
    /// fragment specifier, e.g. the <c>:</c> in <c>$cond:expr</c>.
    /// </summary>
    Colon,

    /// <summary>
    /// A run of literal target-language syntax matched verbatim (keywords, punctuation,
    /// operators, etc.), e.g. <c>while</c>, <c>(</c>, <c>)</c>, <c>{</c>, <c>}</c>, <c>+</c> in the
    /// examples in the solution README. Collapsed into one generic token because the meta-lexer
    /// cannot know a target language's punctuation set ahead of time. A reserved meta character
    /// (<c>$</c>, <c>:</c>, <c>=</c>, <c>"</c>) can appear in a terminal by escaping it with <c>\</c>
    /// (and <c>\\</c> for a literal backslash); the escapes are resolved in the token's value.
    /// </summary>
    Terminal,

    /// <summary>
    /// Marks the end of the source text. Always the last token produced for a given input, so the
    /// parser can uniformly look ahead without special-casing running out of tokens.
    /// </summary>
    EndOfFile,

    /// <summary>
    /// Source text that could not be lexed into any other token kind, e.g. a line with no <c>=</c>
    /// separating a rule name from its pattern, or a rule whose pattern is not a well-formed quoted
    /// body. Used to surface malformed input rather than silently dropping it. Its
    /// <see cref="Token.value"/> is always the full offending source line and its
    /// <see cref="Token.column"/> is <c>0</c>, so a consumer can recover the exact input uniformly.
    /// </summary>
    Unknown,
}
