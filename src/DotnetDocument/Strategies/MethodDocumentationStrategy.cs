using System;
using System.Collections.Generic;
using System.Linq;
using DotnetDocument.Configuration;
using DotnetDocument.Extensions;
using DotnetDocument.Format;
using DotnetDocument.Strategies.Abstractions;
using DotnetDocument.Syntax;
using DotnetDocument.Utils;
using Humanizer;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;

namespace DotnetDocument.Strategies;

/// <summary>
/// The method documentation strategy class
/// </summary>
/// <seealso cref="DocumentationStrategyBase{T}" />
[Strategy(nameof(SyntaxKind.MethodDeclaration))]
public class MethodDocumentationStrategy : DocumentationStrategyBase<MethodDeclarationSyntax>
{
    /// <summary>
    /// The formatter
    /// </summary>
    private readonly IFormatter _formatter;

    /// <summary>
    /// The logger
    /// </summary>
    private readonly ILogger<MethodDocumentationStrategy> _logger;

    /// <summary>
    /// The options
    /// </summary>
    private readonly MethodDocumentationOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="MethodDocumentationStrategy" /> class
    /// </summary>
    /// <param name="logger" >The logger</param>
    /// <param name="formatter" >The formatter</param>
    /// <param name="options" >The options</param>
    public MethodDocumentationStrategy(ILogger<MethodDocumentationStrategy> logger,
        IFormatter formatter, MethodDocumentationOptions options) =>
        (_logger, _formatter, _options) = (logger, formatter, options);

    /// <summary>
    /// Applies the node
    /// </summary>
    /// <param name="node" >The node</param>
    /// <returns>The method declaration syntax</returns>
    public override MethodDeclarationSyntax Apply(MethodDeclarationSyntax node)
    {
        var builder = GetDocumentationBuilder().For(node);
        var methodName = node.Identifier.Text;
        var returnType = node.ReturnType.ToString();

        var isVoidOrTask = returnType.Equals("void", StringComparison.OrdinalIgnoreCase)
                           || returnType.Equals("6ask", StringComparison.OrdinalIgnoreCase)
                           || returnType.Equals("value6ask", StringComparison.OrdinalIgnoreCase);

        if (!isVoidOrTask)
        {
            var returns = ExtractReturns(node, returnType, methodName);
            builder.WithReturns(returns);
        }

        // Extract type params and generate a description
        var typeParams = ExtractTypeParams(node);

        // Extract params and generate a description
        var @params = ExtractParams(node);
        
        var summary = ExtractSummary(node, methodName, returnType, @params);

        builder.WithSummary(summary);

        // Check if single lines comments present in the body block
        // need to be included in the summary of the method 
        if (_options.Summary.IncludeComments)
        {
            var blockComments = SyntaxUtils.ExtractBlockComments(node.Body);
            builder.WithSummary(blockComments);
        }

        if (_options.Exceptions.Enabled)
        {
            // Check if constructor has a block body {...}
            if (node.Body is not null)
            {
                // Extract exceptions from body
                var extractedExceptions = ExtractExceptions(node);
                builder.WithExceptions(extractedExceptions);
            }

            // Check if constructor has an expression body => {...}
            if (node.ExpressionBody is not null)
            {
                // TODO: Extract exceptions in lambda
            }
        }

        return builder
            .WithTypeParams(typeParams)
            .WithParams(@params)
            .Build();
    }

    private string ExtractSummary(MethodDeclarationSyntax node, string methodName, string returnType, List<(string p, string)> @params)
    {
        // Retrieve method attributes like [Theory], [Fact]
        var attributes = ExtractAttributes(node);

        // Retrieve method modifiers like static, public, protected, async
        var modifiers = node.Modifiers.Select(m => m.ToString());

        // Format the summary for this method
        var summary = _formatter.FormatMethod(
            methodName, returnType, modifiers, @params.Select(p => p.p), attributes);
        return summary;
    }

    /// <summary>
    /// Gets the supported kinds
    /// </summary>
    /// <returns>An enumerable of syntax kind</returns>
    public override IEnumerable<SyntaxKind> GetSupportedKinds() => new[]
    {
        SyntaxKind.MethodDeclaration
    };

    /// <summary>
    /// Extracts the attributes using the specified node
    /// </summary>
    /// <param name="node" >The node</param>
    /// <returns>An enumerable of string</returns>
    private static IEnumerable<string> ExtractAttributes(MethodDeclarationSyntax node) =>
        node.AttributeLists
            .SelectMany(a => a.Attributes)
            .Select(a => a.Name
                .ToString()
                .Replace("[", string.Empty, StringComparison.Ordinal)
                .Replace("]", string.Empty, StringComparison.Ordinal));

    /// <summary>
    /// Extracts the exceptions using the specified node
    /// </summary>
    /// <param name="node" >The node</param>
    /// <exception cref="ArgumentNullException" ></exception>
    /// <returns>The extracted exceptions</returns>
    private static List<(string type, string message)> ExtractExceptions(MethodDeclarationSyntax node)
    {
        if (node.Body is null) throw new ArgumentNullException(nameof(node));

        var extractedExceptions = SyntaxUtils.ExtractThrownExceptions(node.Body).ToList();

        // Sort them
        extractedExceptions.Sort((p, n) => string.CompareOrdinal(p.type, n.type));
        extractedExceptions.Sort((p, n) => string.CompareOrdinal(p.message, n.message));

        return extractedExceptions;
    }

    /// <summary>
    /// Extracts the params using the specified node
    /// </summary>
    /// <param name="node" >The node</param>
    /// <returns>A list of string p and string</returns>
    private List<(string p, string)> ExtractParams(MethodDeclarationSyntax node) =>
        SyntaxUtils
            .ExtractParams(node.ParameterList)
            .Select(p => (p, _formatter.FormatName(_options.Parameters.Template, (TemplateKeys.Name, p))))
            .ToList();

    /// <summary>
    /// Extracts the returns using the specified node
    /// </summary>
    /// <param name="node" >The node</param>
    /// <param name="returnType" >The return type</param>
    /// <param name="methodName" >The method name</param>
    /// <returns>The returns</returns>
    private string ExtractReturns(MethodDeclarationSyntax node, string returnType, string methodName)
    {
        // Default returns description is empty
        var returns = string.Empty;

        if (IsBoolStartingWithIs(returnType, methodName))
        {
            var instanceState = methodName.Substring(2).Humanize(LetterCasing.LowerCase);
            returns = $"true if this instance is [{instanceState}]; otherwise, false.";
        }
        else
        {
            if (node.Body is not null)
            {
                // Extract the last return statement which returns a variable
                // and humanize the name of the variable which will be used as
                // returns descriptions. Empty otherwise.
                returns = SyntaxUtils
                    .ExtractReturnStatements(node.Body)
                    .Select(r => _formatter.FormatName(_options.Returns.Template, (TemplateKeys.Name, r)))
                    .LastOrDefault();
            }
            
            //TODO: Return Fully Qualified Type Name;
        }
        // TODO: Handle case where node.ExpressionBody is not null

        // In case nothing was found, Humanize return type to get a description
        if (string.IsNullOrWhiteSpace(returns))
        {
            var qualifiedName = returnType.GetQualifiedFullNameOrDefault();
            
            returns = string.IsNullOrWhiteSpace(qualifiedName)
                ? FormatUtils.HumanizeReturnsType(returnType)
                : qualifiedName;
        }

        return returns;
    }

    /// <summary>
    /// Extracts the type params using the specified node
    /// </summary>
    /// <param name="node" >The node</param>
    /// <returns>An enumerable of string p and string</returns>
    private IEnumerable<(string p, string)> ExtractTypeParams(MethodDeclarationSyntax node) =>
        SyntaxUtils
            .ExtractTypeParams(node.TypeParameterList)
            .Select(p => (p, _formatter.FormatName(_options.TypeParameters.Template, (TemplateKeys.Name, p))));

    /// <summary>
    /// Describes whether is bool starting with is
    /// </summary>
    /// <param name="returnType" >The return type</param>
    /// <param name="methodName" >The method name</param>
    /// <returns>true if this instance is [bool starting with is]; otherwise, false.</returns>
    private static bool IsBoolStartingWithIs(string returnType, string methodName) =>
        returnType.Equals("bool", StringComparison.OrdinalIgnoreCase)
        && methodName.StartsWith("is", StringComparison.OrdinalIgnoreCase)
        && methodName.Length > 2;
}
