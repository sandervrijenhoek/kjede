// <copyright file="Token.cs" company="Kjede">
// This file is part of the Kjede project.
// </copyright>

namespace Kjede.Lexer.Api;

/// <summary>
/// A single lexed unit of a Kjede rule definition.
/// </summary>
/// <param name="type">The kind of token, e.g. <see cref="TokenType.Dollar"/> or <see cref="TokenType.Terminal"/>.</param>
/// <param name="value">
/// The text the token represents, e.g. <c>$</c> or <c>while</c>. For a <see cref="TokenType.Terminal"/>
/// any escape sequences are resolved (so <c>\$</c> yields <c>$</c>); other kinds carry their raw source
/// text. <see cref="column"/> marks the token's raw start, which for an escaped terminal spans more
/// source characters than <c>value</c> has — <see cref="Token"/> deliberately records no length.
/// </param>
/// <param name="line">The line on which the token starts.</param>
/// <param name="column">The column on which the token starts.</param>
public record Token(TokenType type, string value, int line, int column);
