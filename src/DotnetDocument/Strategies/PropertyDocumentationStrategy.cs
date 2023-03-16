using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DotnetDocument.Configuration;
using DotnetDocument.Extensions;
using DotnetDocument.Format;
using DotnetDocument.Strategies.Abstractions;
using Humanizer;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;

namespace DotnetDocument.Strategies;

/// <summary>
/// The property documentation strategy class
/// </summary>
/// <seealso cref="DocumentationStrategyBase{T}" />
[Strategy(nameof(SyntaxKind.PropertyDeclaration))]
public class PropertyDocumentationStrategy : DocumentationStrategyBase<PropertyDeclarationSyntax>
{
    /// <summary>
    /// The formatter
    /// </summary>
    private readonly IFormatter _formatter;

    /// <summary>
    /// The logger
    /// </summary>
    private readonly ILogger<PropertyDocumentationStrategy> _logger;

    /// <summary>
    /// The options
    /// </summary>
    private readonly PropertyDocumentationOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="PropertyDocumentationStrategy" /> class
    /// </summary>
    /// <param name="logger" >The logger</param>
    /// <param name="formatter" >The formatter</param>
    /// <param name="options" >The options</param>
    public PropertyDocumentationStrategy(ILogger<PropertyDocumentationStrategy> logger,
        IFormatter formatter, PropertyDocumentationOptions options) =>
        (_logger, _formatter, _options) = (logger, formatter, options);

    /// <summary>
    /// Applies the node
    /// </summary>
    /// <param name="node" >The node</param>
    /// <returns>The property declaration syntax</returns>
    public override PropertyDeclarationSyntax Apply(PropertyDeclarationSyntax node)
    {
        // Retrieve constructor name
        var propertyName = node.Identifier.Text;

        // Humanize the constructor name
        var humanizedPropertyName = propertyName
            .Humanize()
            .ToLower(CultureInfo.InvariantCulture);

        var accessorsDescription = ExtractAccessorsDescription(node);

        var summary = new List<string>
        {
            // Declare the summary by using the template from configuration
            _options.Summary.Template
                .Replace(TemplateKeys.Accessors, accessorsDescription, StringComparison.Ordinal)
                .Replace(TemplateKeys.Name, humanizedPropertyName, StringComparison.Ordinal)
        };

        // Check if constructor has an expression body => {...}
        if (node.ExpressionBody is not null)
        {
        }

        var builder = GetDocumentationBuilder()
            .For(node)
            .WithSummary(summary.ToArray());

        if (_options.Value.Enabled)
        {
            var value = node.Type
                .ToString();

            value = string.IsNullOrWhiteSpace(value)
                ? BuildImprovisedValue(accessorsDescription, humanizedPropertyName)
                : value.GetQualifiedFullNameOrDefault();

            builder.WithValue(value);
        }

        return builder.Build();
    }

    /// <summary>
    /// Gets the supported kinds
    /// </summary>
    /// <returns>An enumerable of syntax kind</returns>
    public override IEnumerable<SyntaxKind> GetSupportedKinds() => new[]
    {
        SyntaxKind.PropertyDeclaration
    };

    /// <summary>
    /// Builds the improvised value using the specified accessors description
    /// </summary>
    /// <param name="accessorsDescription" >The accessors description</param>
    /// <param name="humanizedPropertyName" >The humanized property name</param>
    /// <returns>The string</returns>
    private string BuildImprovisedValue(string accessorsDescription, string humanizedPropertyName) =>
        _options.Value.Template
            .Replace(TemplateKeys.Accessors, accessorsDescription, StringComparison.Ordinal)
            .Replace(TemplateKeys.Name, humanizedPropertyName, StringComparison.Ordinal);

    /// <summary>
    /// Extracts the accessors description using the specified node
    /// </summary>
    /// <param name="node" >The node</param>
    /// <returns>String</returns>
    private string ExtractAccessorsDescription(PropertyDeclarationSyntax node)
    {
        var accessors = node.AccessorList?.Accessors
            .Select(a => _formatter.ConjugateThirdPersonSingular(a.Keyword.Text))
            .ToList();

        if (accessors is not null && accessors.Any())
        {
            return string.Join(" or ", accessors)
                .ToLower(CultureInfo.InvariantCulture)
                .Humanize();
        }

        return _formatter.ConjugateThirdPersonSingular("Get");
    }
}
