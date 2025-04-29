using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VulcanAI.Knowledge;

/// <summary>
/// Represents a collection of knowledge items with associated relevance scores.
/// </summary>
/// <remarks>
/// This class maintains a sorted collection of knowledge items where each item
/// has an associated relevance score. The collection is always kept sorted in
/// descending order by score. It supports serialization to and from JSON format.
/// The collection is thread-safe for read operations, but write operations
/// should be synchronized externally if used in a multi-threaded context.
/// </remarks>
public class KnowledgeCollection : IEnumerable<(double Score, Knowledge Knowledge)>
{
    private readonly List<(double Score, Knowledge Knowledge)> _knowledgeItems = new();

    /// <summary>
    /// Gets the number of knowledge items in the collection.
    /// </summary>
    /// <value>The total count of knowledge items.</value>
    public int Count => _knowledgeItems.Count;

    /// <summary>
    /// Gets the knowledge item at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the item to retrieve.</param>
    /// <returns>A tuple containing the score and knowledge item at the specified index.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is out of range.</exception>
    public (double Score, Knowledge Knowledge) this[int index]
    {
        get
        {
            if (index < 0 || index >= _knowledgeItems.Count)
                throw new ArgumentOutOfRangeException(nameof(index), "Index was out of range.");
            return _knowledgeItems[index];
        }
    }

    /// <summary>
    /// Adds a new knowledge item to the collection with the specified relevance score.
    /// </summary>
    /// <param name="knowledge">The knowledge item to add.</param>
    /// <param name="score">The relevance score of the knowledge item.</param>
    /// <exception cref="ArgumentNullException">Thrown when knowledge is null.</exception>
    /// <exception cref="ArgumentException">Thrown when score is not a valid number.</exception>
    /// <remarks>
    /// The item is inserted in the correct position to maintain the collection's
    /// sorted order by score in descending order. If the score is NaN or infinity,
    /// it will be treated as 0.0.
    /// </remarks>
    public void Add(Knowledge knowledge, double score)
    {
        if (knowledge == null)
            throw new ArgumentNullException(nameof(knowledge));

        if (double.IsNaN(score) || double.IsInfinity(score))
            score = 0.0;

        var item = (score, knowledge);
        var index = _knowledgeItems.BinarySearch(item, new ScoreComparer());
        if (index < 0) index = ~index;
        _knowledgeItems.Insert(index, item);
    }

    /// <summary>
    /// Removes all knowledge items from the collection.
    /// </summary>
    public void Clear()
    {
        _knowledgeItems.Clear();
    }

    /// <summary>
    /// Gets a slice of the knowledge collection with the highest relevance scores.
    /// </summary>
    /// <param name="count">The maximum number of items to return.</param>
    /// <returns>A new collection containing the top items by score.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when count is negative.</exception>
    public KnowledgeCollection GetTopItems(int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Count cannot be negative.");

        var result = new KnowledgeCollection();
        foreach (var item in _knowledgeItems.Take(count))
        {
            result.Add(item.Knowledge, item.Score);
        }
        return result;
    }

    /// <summary>
    /// Serializes the knowledge collection to a JSON string.
    /// </summary>
    /// <returns>A JSON string representation of the knowledge collection.</returns>
    /// <remarks>
    /// The JSON output is formatted with indentation for better readability.
    /// The serialization includes all knowledge items with their scores.
    /// </remarks>
    public string SerializeToJson()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };
        return JsonSerializer.Serialize(_knowledgeItems, options);
    }

    /// <summary>
    /// Deserializes a knowledge collection from a JSON string.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>A new instance of <see cref="KnowledgeCollection"/> containing the deserialized items.</returns>
    /// <exception cref="ArgumentNullException">Thrown when json is null.</exception>
    /// <exception cref="JsonException">Thrown when the JSON string is invalid.</exception>
    public static KnowledgeCollection DeserializeFromJson(string json)
    {
        if (json == null)
            throw new ArgumentNullException(nameof(json));

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
        var items = JsonSerializer.Deserialize<List<(double Score, Knowledge Knowledge)>>(json, options);
        var collection = new KnowledgeCollection();
        foreach (var item in items)
        {
            collection.Add(item.Knowledge, item.Score);
        }
        return collection;
    }

    /// <summary>
    /// Returns an enumerator that iterates through the collection.
    /// </summary>
    /// <returns>An enumerator that can be used to iterate through the collection.</returns>
    public IEnumerator<(double Score, Knowledge Knowledge)> GetEnumerator()
    {
        return _knowledgeItems.GetEnumerator();
    }

    /// <summary>
    /// Returns an enumerator that iterates through the collection.
    /// </summary>
    /// <returns>An enumerator that can be used to iterate through the collection.</returns>
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>
    /// Comparer class used to maintain the collection's sorted order by score.
    /// </summary>
    private class ScoreComparer : IComparer<(double Score, Knowledge Knowledge)>
    {
        /// <summary>
        /// Compares two knowledge items by their scores.
        /// </summary>
        /// <param name="x">The first knowledge item to compare.</param>
        /// <param name="y">The second knowledge item to compare.</param>
        /// <returns>
        /// A value that indicates the relative order of the objects being compared.
        /// The return value has these meanings:
        /// Less than zero: x is less than y.
        /// Zero: x equals y.
        /// Greater than zero: x is greater than y.
        /// </returns>
        public int Compare((double Score, Knowledge Knowledge) x, (double Score, Knowledge Knowledge) y)
        {
            return y.Score.CompareTo(x.Score); // Sort in descending order
        }
    }
} 