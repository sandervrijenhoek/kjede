// <copyright file="ILexer.cs" company="Kjede">
// This file is part of the Kjede project.
// </copyright>

namespace Kjede.Lexer.Api;

using System.Collections.Immutable;

/// <summary>
/// Turns Kjede source text into the flat <see cref="Token"/> stream the parser consumes.
/// </summary>
public interface ILexer
{
    /// <summary>
    /// Lexes the full contents of a Kjede rule definition file.
    /// </summary>
    /// <param name="source">The raw Kjede source text to tokenize.</param>
    /// <returns>The tokens found in <paramref name="source"/>, in source order.</returns>
    public ImmutableArray<Token> Tokenize(string source);
}
