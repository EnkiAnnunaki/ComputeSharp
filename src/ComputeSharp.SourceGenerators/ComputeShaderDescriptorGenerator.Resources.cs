using System;
using System.Collections.Immutable;
using ComputeSharp.SourceGeneration.Extensions;
using ComputeSharp.SourceGeneration.Helpers;
using ComputeSharp.SourceGeneration.Mappings;
using ComputeSharp.SourceGeneration.Models;
using ComputeSharp.SourceGeneration.SyntaxProcessors;
using ComputeSharp.SourceGenerators.Models;
using Microsoft.CodeAnalysis;
using static ComputeSharp.SourceGeneration.Diagnostics.DiagnosticDescriptors;

namespace ComputeSharp.SourceGenerators;

/// <inheritdoc/>
partial class ComputeShaderDescriptorGenerator
{
    /// <summary>
    /// A helper with all logic to generate the resources loading code.
    /// </summary>
    private static partial class Resources
    {
        /// <summary>
        /// Gathers info on all resources captured by a given shader type.
        /// </summary>
        /// <param name="diagnostics">The collection of produced <see cref="DiagnosticInfo"/> instances.</param>
        /// <param name="compilation">The input <see cref="Compilation"/> object currently in use.</param>
        /// <param name="structDeclarationSymbol">The current shader type being explored.</param>
        /// <param name="isImplicitTextureUsed">Indicates whether the current shader uses an implicit texture.</param>
        /// <param name="constantBufferSizeInBytes">The size of the shader constant buffer.</param>
        /// <param name="resources">The sequence of <see cref="ResourceInfo"/> instances for all captured resources.</param>
        /// <param name="resourceDescriptors">The sequence of <see cref="ResourceDescriptor"/> instances for all captured resources.</param>
        public static void GetInfo(
            ImmutableArrayBuilder<DiagnosticInfo> diagnostics,
            Compilation compilation,
            ITypeSymbol structDeclarationSymbol,
            bool isImplicitTextureUsed,
            int constantBufferSizeInBytes,
            out ImmutableArray<ResourceInfo> resources,
            out ImmutableArray<ResourceDescriptor> resourceDescriptors)
        {
            using ImmutableArrayBuilder<ResourceInfo> resourceBuilder = new();
            using ImmutableArrayBuilder<ResourceDescriptor> resourceDescriptorBuilder = new();

            int constantBufferOffset = 1;
            int readOnlyResourceOffset = 0;
            int readWriteResourceOffset = 0;

            // Add the implicit texture descriptor, if needed
            if (isImplicitTextureUsed)
            {
                resourceDescriptorBuilder.Add(new ResourceDescriptor(1, readWriteResourceOffset++));
            }

            foreach (ISymbol memberSymbol in structDeclarationSymbol.GetMembers())
            {
                // Only process instance fields
                if (memberSymbol is not IFieldSymbol { Type: INamedTypeSymbol { IsStatic: false }, IsConst: false, IsStatic: false, IsFixedSizeBuffer: false, IsImplicitlyDeclared: false } fieldSymbol)
                {
                    continue;
                }

                // Skip fields of not accessible types (the analyzer will handle this just like other fields)
                if (!fieldSymbol.Type.IsAccessibleFromCompilationAssembly(compilation))
                {
                    continue;
                }

                // Try to get the name to use for the field and the accessor
                if (!ConstantBufferSyntaxProcessor.TryGetFieldAccessorName(fieldSymbol, out string? fieldName, out string? unspeakableName))
                {
                    continue;
                }

                string typeName = fieldSymbol.Type.GetFullyQualifiedMetadataName();

                // Check if the field is a resource (note: resources can only be top level fields)
                if (HlslKnownTypes.IsTypedResourceType(typeName))
                {
                    resourceBuilder.Add(new ResourceInfo(fieldName, unspeakableName));

                    // Populate the resource descriptors as well
                    if (HlslKnownTypes.IsConstantBufferType(typeName))
                    {
                        resourceDescriptorBuilder.Add(new ResourceDescriptor(2, constantBufferOffset++));
                    }
                    else if (HlslKnownTypes.IsReadOnlyTypedResourceType(typeName))
                    {
                        resourceDescriptorBuilder.Add(new ResourceDescriptor(0, readOnlyResourceOffset++));
                    }
                    else
                    {
                        resourceDescriptorBuilder.Add(new ResourceDescriptor(1, readWriteResourceOffset++));
                    }
                }
            }

            resources = resourceBuilder.ToImmutable();
            resourceDescriptors = resourceDescriptorBuilder.ToImmutable();

            // A shader root signature has a maximum size of 64 DWORDs, so 256 bytes.
            // Loaded values in the root signature have the following costs:
            //  - Root constants cost 1 DWORD each, since they are 32-bit values.
            //  - Descriptor tables cost 1 DWORD each.
            //  - Root descriptors (64-bit GPU virtual addresses) cost 2 DWORDs each.
            // So here we check whether the current signature respects that constraint,
            // and emit a build error otherwise. For more info on this, see the docs here:
            // https://docs.microsoft.com/windows/win32/direct3d12/root-signature-limits.
            if ((constantBufferSizeInBytes / sizeof(int)) + resourceBuilder.Count > 64)
            {
                diagnostics.Add(ShaderDispatchDataSizeExceeded, structDeclarationSymbol, structDeclarationSymbol);
            }
        }
    }
}