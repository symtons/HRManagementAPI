using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace HRManagementAPI
{
    /// <summary>
    /// Operation filter to handle file upload parameters in Swagger UI
    /// </summary>
    public class FileUploadOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var fileParameters = context.MethodInfo.GetParameters()
                .Where(p => p.ParameterType == typeof(IFormFile) ||
                           p.ParameterType == typeof(IFormFileCollection) ||
                           p.ParameterType == typeof(IEnumerable<IFormFile>))
                .ToList();

            if (!fileParameters.Any())
                return;

            operation.Parameters?.Clear();

            operation.RequestBody = new OpenApiRequestBody
            {
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["multipart/form-data"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = "object",
                            Properties = fileParameters.ToDictionary(
                                p => p.Name ?? "file",
                                p => new OpenApiSchema
                                {
                                    Type = "string",
                                    Format = "binary"
                                }
                            ),
                            Required = fileParameters
                                .Where(p => !p.IsOptional)
                                .Select(p => p.Name ?? "file")
                                .ToHashSet()
                        }
                    }
                }
            };
        }
    }
}