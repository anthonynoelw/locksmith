namespace Api.OpenApi;

using Asp.Versioning.ApiExplorer;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

/// <summary>
/// Sets the OpenAPI document <c>info.version</c> and <c>info.title</c> to match
/// the API version for which the document was generated.
/// </summary>
internal sealed class ApiVersionDocumentTransformer(IApiVersionDescriptionProvider provider)
    : IOpenApiDocumentTransformer
{
    /// <summary>
    /// Applies version-specific <c>info.version</c> and <c>info.title</c> to the document.
    /// </summary>
    /// <param name="document">The OpenAPI document being generated.</param>
    /// <param name="context">Context including the document name used to match the version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A completed task.</returns>
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        ApiVersionDescription? description = provider.ApiVersionDescriptions
            .FirstOrDefault(d => d.GroupName == context.DocumentName);

        if (description is not null)
        {
            document.Info.Version = description.ApiVersion.ToString();
            document.Info.Title = $"API {description.GroupName}";

            if (description.IsDeprecated)
            {
                document.Info.Description = "(This API version has been deprecated.)";
            }
        }

        return Task.CompletedTask;
    }
}
