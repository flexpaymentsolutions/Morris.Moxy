﻿using Microsoft.CodeAnalysis;
using Morris.Moxy.Metas.Classes;
using Morris.Moxy.Metas.ProjectInformation;
using Morris.Moxy.Metas.Templates;
using Morris.Moxy.SourceGenerators;
using Morris.Moxy.Extensions;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Morris.Moxy;

[Generator]
public class RoslynIncrementalGenerator : IIncrementalGenerator
{
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		IncrementalValueProvider<ProjectInformationMeta> projectInformationProvider = context.CreateProjectInformationProvider();
		IncrementalValuesProvider<ValidatedResult<ParsedTemplate>> parsedTemplatesProvider = context.CreateParsedTemplatesProvider();
		IncrementalValuesProvider<ValidatedResult<CompiledTemplate>> compiledTemplatesProvider = parsedTemplatesProvider.CreateCompiledTemplateProvider();
		IncrementalValuesProvider<ClassMeta> classMetaProvider = context.CreateClassMetasProvider(projectInformationProvider, parsedTemplatesProvider);

		context.RegisterSourceOutput(
			source: parsedTemplatesProvider.Combine(projectInformationProvider),
			static (productionContext, input) =>
			{
				ValidatedResult<ParsedTemplate> parsedTemplateResult = input.Left;
				if (parsedTemplateResult.Failure)
					productionContext.AddCompilationErrors(parsedTemplateResult.FilePath, parsedTemplateResult.CompilationErrors);
				else
				{
					ProjectInformationMeta projectInfo = input.Right;
					TemplateAttributeSourceGenerator.Generate(productionContext, projectInfo, parsedTemplateResult.Value);
				}
			});
		
		context.RegisterSourceOutput(
			source: compiledTemplatesProvider.Combine(classMetaProvider.Collect()),
			static (productionContext, input) =>
			{
				ValidatedResult<CompiledTemplate> compiledTemplateResult = input.Left;
				if (compiledTemplateResult.Failure)
					productionContext.AddCompilationErrors(compiledTemplateResult.FilePath, compiledTemplateResult.CompilationErrors);
				else
				{
					ImmutableArray<ClassMeta> classMetas = input.Right
						.Where(x => x.PossibleTemplates
									.Any(p => p.Name == compiledTemplateResult.Value.Name)).ToImmutableArray();
					ClassSourceGenerator.Generate(productionContext, compiledTemplateResult.Value, classMetas);
				}
			});
	}

	//RoslynIncrementalGenerator.AttachDebugger();
	[Conditional("DEBUG")]
	public static void AttachDebugger()
	{
#pragma warning disable RS1035
		Debugger.Launch();
#pragma warning restore RS1035

	}
}
