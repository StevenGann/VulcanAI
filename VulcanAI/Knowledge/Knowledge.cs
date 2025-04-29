using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VulcanAI.Knowledge;

/// <summary>
/// Represents a piece of knowledge with its content and origin information.
/// </summary>
/// <remarks>
/// This record type is immutable and represents a single piece of knowledge
/// that can be stored and retrieved from the knowledge store. The content
/// represents the actual knowledge, while the origin provides metadata about
/// where this knowledge came from.
/// </remarks>
public record Knowledge
{
    /// <summary>
    /// Gets the content of the knowledge.
    /// </summary>
    /// <value>The textual content representing the knowledge.</value>
    public string Content { get; init; }

    /// <summary>
    /// Gets the origin of the knowledge.
    /// </summary>
    /// <value>A string describing where this knowledge came from.</value>
    public string Origin { get; init; }

    /// <summary>
    /// Gets the timestamp when this knowledge was created.
    /// </summary>
    /// <value>The creation timestamp in UTC.</value>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Knowledge"/> record.
    /// </summary>
    /// <param name="content">The content of the knowledge.</param>
    /// <param name="origin">The origin of the knowledge.</param>
    /// <exception cref="ArgumentNullException">Thrown when content or origin is null.</exception>
    /// <exception cref="ArgumentException">Thrown when content or origin is empty or whitespace.</exception>
    public Knowledge(string content, string origin)
    {
        Content = content?.Trim() ?? throw new ArgumentNullException(nameof(content));
        Origin = origin?.Trim() ?? throw new ArgumentNullException(nameof(origin));
        CreatedAt = DateTimeOffset.UtcNow;

        if (string.IsNullOrWhiteSpace(Content))
            throw new ArgumentException("Content cannot be empty or whitespace.", nameof(content));
        if (string.IsNullOrWhiteSpace(Origin))
            throw new ArgumentException("Origin cannot be empty or whitespace.", nameof(origin));
    }
}