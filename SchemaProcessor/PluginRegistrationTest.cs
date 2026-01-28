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
                // Input: Payload JSON
                // -----------------------------
                var payloadJson = context.InputParameters.Contains("PayloadJson")
                    ? context.InputParameters["PayloadJson"] as string
                    : null;

                if (string.IsNullOrWhiteSpace(payloadJson))
                {
                    throw new InvalidPluginExecutionException("PayloadJson input parameter is missing.");
                }

                var payload = JObject.Parse(payloadJson);
                tracing.Trace("Payload loaded");

                // -----------------------------
                // Input: Schema JSON
                // -----------------------------
                var schemaJson = context.InputParameters.Contains("SchemaJson")
                    ? context.InputParameters["SchemaJson"] as string
                    : null;

                if (string.IsNullOrWhiteSpace(schemaJson))
                {
                    throw new InvalidPluginExecutionException("SchemaJson input parameter is missing.");
                }

                var schemaPayload = JObject.Parse(schemaJson);
                tracing.Trace("Schema JSON loaded");

                // -----------------------------
                // Input: RelatedEntityId
                // -----------------------------
                var relatedEntityId = context.InputParameters.Contains("RelatedEntityId")
                    ? context.InputParameters["RelatedEntityId"] as string
                    : "";

                tracing.Trace($"RelatedEntityId: {relatedEntityId}");

                // -----------------------------
                // Build payload lookup (KEY OPTIMIZATION)
                // -----------------------------
                var payloadLookup = new Dictionary<string, JToken>(StringComparer.OrdinalIgnoreCase);

                foreach (var prop in payload.Properties())
                {
                    payloadLookup[prop.Name] = prop.Value;
                }

                tracing.Trace($"Payload properties indexed: {payloadLookup.Count}");

                // -----------------------------
                // Retrieve ALL schema records
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
                // Single loop over schema
                // -----------------------------
                var results = new List<object>();

                foreach (var entity in schemaCollection.Entities)
                {
                    var fieldName = entity.GetAttributeValue<string>("entres_field");

                    if (string.IsNullOrWhiteSpace(fieldName))
                    {
                        continue;
                    }

                    // O(1) lookup instead of inner loop
                    if (!payloadLookup.TryGetValue(fieldName, out var token))
                    {
                        continue;
                    }

                    tracing.Trace($"MATCH FOUND: {fieldName} = {token}");

                    results.Add(new
                    {
                        withCoreSchemaName = fieldName,
                        value = token.Type == JTokenType.Null ? null : token.ToObject<object>(),
                        withCoreEntity = schemaPayload["header"]?["type"]?.ToString(),
                        entityUri = schemaPayload["uri"]?.ToString(),
                        entityCreatedAt = schemaPayload["versioning"]?["createdAt"]?.Value<long>() ?? 0,
                        relatedEntityId = relatedEntityId
                    });
                }

                // -----------------------------
                // Output
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
