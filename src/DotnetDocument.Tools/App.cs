using System;
using System.IO;
using System.Threading.Tasks;
using DotnetDocument.Configuration;
using DotnetDocument.Strategies.Abstractions;
using DotnetDocument.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotnetDocument.Tools
{
    public class App
    {
        private readonly ILogger<App> _logger;
        private readonly IDocumentationStrategy.ServiceResolver _resolver;
        private readonly DotnetDocumentOptions _dotnetDocumentSettings;
        private readonly DocumentationSyntaxWalker _walker;

        public App(ILogger<App> logger, IDocumentationStrategy.ServiceResolver resolver,
            DocumentationSyntaxWalker walker, IOptions<DotnetDocumentOptions> appSettings) =>
            (_logger, _resolver, _walker, _dotnetDocumentSettings) = (logger, resolver, walker, appSettings.Value);

        public async Task Run(string[] args)
        {
            // Read file content
            var fileContent = await File.ReadAllTextAsync(args[0]);

            // Declare a new CSharp syntax tree
            var tree = CSharpSyntaxTree.ParseText(fileContent,
                new CSharpParseOptions(documentationMode: DocumentationMode.Parse));

            // Get the compilation unit root
            var root = tree.GetCompilationUnitRoot();

            _walker.Visit(root);

            foreach (var node in _walker.NodesWithoutXmlDoc)
            {
                _logger.LogDebug("no doc: {NodeName}", node.GetType().FullName);
            }

            foreach (var node in _walker.NodesWithXmlDoc)
            {
                _logger.LogDebug("has doc: {NodeName}", node.GetType().FullName);
            }

            var changedSyntaxTree = root.ReplaceNodes(_walker.NodesWithoutXmlDoc,
                (node, syntaxNode) => _resolver(syntaxNode.Kind())?.Apply(syntaxNode) ?? syntaxNode);

            File.WriteAllText($"{args[0].Replace(".cs", "-")}{Guid.NewGuid()}.cs", changedSyntaxTree.ToFullString());

            _logger.LogDebug("completed");

            await Task.CompletedTask;
        }
    }
}