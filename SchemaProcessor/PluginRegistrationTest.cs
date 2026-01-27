using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MyCompany.Plugins
{
    public class PluginRegistrationTest : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var service = serviceFactory.CreateOrganizationService(context.UserId);

            tracing.Trace("=== PluginRegistrationTest START ===");

            try
            {
                // -----------------------------
                // 1. Parse PayloadJson as an ARRAY
                // -----------------------------
                var payloadJson = context.InputParameters.Contains("PayloadJson")
                    ? context.InputParameters["PayloadJson"] as string
                    : null;

                if (string.IsNullOrWhiteSpace(payloadJson))
                    throw new InvalidPluginExecutionException("PayloadJson input parameter is missing.");

                var payloadArray = JArray.Parse(payloadJson);
                tracing.Trace($"Payload array loaded with {payloadArray.Count} items");

                // Flatten all properties into a single list
                var allProperties = new List<JProperty>();
                foreach (var item in payloadArray)
                {
                    if (item is JObject obj)
                        allProperties.AddRange(obj.Properties());
                }
                tracing.Trace($"Total properties to process: {allProperties.Count}");
                tracing.Trace($"Flattened properties: {JsonConvert.SerializeObject(allProperties)}");

                // -----------------------------
                // 2. Parse second JSON (SchemaJson)
                // -----------------------------
                var secondJson = context.InputParameters.Contains("SchemaJson")
                    ? context.InputParameters["SchemaJson"] as string
                    : null;

                if (string.IsNullOrWhiteSpace(secondJson))
                    throw new InvalidPluginExecutionException("SchemaJson input parameter is missing.");

                var secondPayload = JObject.Parse(secondJson);
                tracing.Trace("Second JSON loaded");

                // -----------------------------
                // 3. Retrieve ALL schema records
                // -----------------------------
                var query = new QueryExpression("entres_entityresolutionschematable")
                {
                    ColumnSet = new ColumnSet(
                        "entres_field",
                        "entres_withcoreentity",
                        "entres_datatype",
                        "entres_order"
                    )
                };

                var schemaCollection = service.RetrieveMultiple(query);
                tracing.Trace($"Total schema records retrieved: {schemaCollection.Entities.Count}");

                // -----------------------------
                // 4. Nested foreach: iterate schema + flattened payload properties
                // -----------------------------
                var results = new List<object>();

                foreach (var entity in schemaCollection.Entities)
                {
                    var fieldName = entity.GetAttributeValue<string>("entres_field");
                    var withCoreSchemaName = entity.GetAttributeValue<string>("entres_withcoreentity");

                    tracing.Trace($"Checking schema field: {fieldName}");

                    if (string.IsNullOrWhiteSpace(fieldName))
                    {
                        tracing.Trace("Skipping empty field");
                        continue;
                    }

                    foreach (var prop in allProperties)
                    {
                        if (string.Equals(prop.Name, fieldName, StringComparison.OrdinalIgnoreCase))
                        {
                            tracing.Trace($"MATCH FOUND: {fieldName} = {prop.Value}");

                            results.Add(new
                            {
                                withCoreSchemaName = fieldName,
                                value = prop.Value.Type == JTokenType.Null ? null : prop.Value.ToObject<object>(),
                                withCoreEntity = secondPayload["header"]?["type"]?.ToString(),
                                entityUri = secondPayload["uri"]?.ToString(),
                                entityCreatedAt = secondPayload["versioning"]?["createdAt"]?.Value<long>() ?? 0,
                                relatedEntityId = ""
                            });

                            break;
                        }
                    }
                }

                // -----------------------------
                // 5. Output
                // -----------------------------
                var resultJson = JsonConvert.SerializeObject(results);
                tracing.Trace($"Result JSON: {resultJson}");
                context.OutputParameters["ResultJson"] = resultJson;

                tracing.Trace("=== PluginRegistrationTest SUCCESS ===");
            }
            catch (Exception ex)
            {
                tracing.Trace("=== PluginRegistrationTest ERROR ===");
                tracing.Trace(ex.ToString());
                throw;
            }
        }
    }
}
