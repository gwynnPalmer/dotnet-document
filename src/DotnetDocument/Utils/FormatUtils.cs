using System;
using System.Linq;
using DotnetDocument.Extensions;
using Humanizer;

namespace DotnetDocument.Utils;

/// <summary>
/// The format utils class
/// </summary>
public static class FormatUtils
{
    /// <summary>
    /// Humanizes the returns type using the specified returns type
    /// </summary>
    /// <param name="returnsType" >The returns type</param>
    /// <returns>The description</returns>
    public static string HumanizeReturnsType(string returnsType)
    {
        // Trim returns type just in case
        returnsType = returnsType.Trim();
        var description = string.Empty;

        var isEmpty = returnsType.Equals("void", StringComparison.OrdinalIgnoreCase)
                      || returnsType.Equals("task", StringComparison.OrdinalIgnoreCase)
                      || returnsType.Equals("valuetask", StringComparison.OrdinalIgnoreCase);

        // Nothing to return
        if (isEmpty) return string.Empty;

        // If the return starts with task
        if ((returnsType.StartsWith("Task<") || returnsType.StartsWith("ValueTask<")) && returnsType.EndsWith(">"))
        {
            return $"A {returnsType} representing the asynchronous operation";
        }
        
        // Handle arrays.
        if (returnsType.Contains("[]")) returnsType = returnsType.Replace("[]", " array ");

        if (returnsType.Contains("<") && returnsType.Contains(">"))
        {
            // Retrieve the generic type
            var keywords = returnsType
                .Replace(",", " and ")
                .Split(new[] { '<', '>' }, StringSplitOptions.TrimEntries)
                .Select(k => k.Humanize());

            var genericType = RemoveSingleCharsInPhrase(keywords.FirstOrDefault() ?? string.Empty);

            var prefix = genericType
                .FirstOrDefault()
                .IsVowel() ? "an" : "a";

            // This is a generic type
            description += $" {prefix} {genericType} of {string.Join(" ", keywords.Skip(1))}";
        }
        else
        {
            var humanizeReturnsType = RemoveSingleCharsInPhrase(returnsType.Humanize());
            var prefix = "the";

            // if (humanizeReturnsType.FirstOrDefault().IsVowel() is true)
            // {
            //     prefix = "an";
            // }

            // It is a simple object
            description += $" {prefix} {humanizeReturnsType}";
        }

        // Remove single chars between white spaces
        description = description
            .Humanize()
            .FirstCharToUpper();

        return description;
    }

    /// <summary>
    /// Removes the single chars in phrase using the specified text
    /// </summary>
    /// <param name="text" >The text</param>
    /// <returns>The string</returns>
    public static string RemoveSingleCharsInPhrase(string text) =>
        string.Join(" ", text
            .Split(" ")
            .Where(t => t.Length > 1));
}
