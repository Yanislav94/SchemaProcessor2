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
            var context =
                (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));

            var tracing =
                (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            var serviceFactory =
                (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));

            var service = serviceFactory.CreateOrganizationService(context.UserId);

            tracing.Trace("=== PluginRegistrationTest START ===");

            try
            {
                // -----------------------------
                // Payload JSON
                // -----------------------------
                var payloadJson = @"{
                  ""name"": ""Thames River Topaz Fund - USD"",
                  ""assetClass"": ""/classification/AssetClass/1"",
                  ""strategy"": ""/classification/PrimaryStrategy/77"",
                  ""vintageYear"": null,
                  ""managerName"": null,
                  ""type"": ""Open End""
                }";

                var payload = JObject.Parse(payloadJson);
                tracing.Trace("Payload loaded");

                // -----------------------------
                // Retrieve ALL schema records (simple, single query, assume <5000)
                // -----------------------------
                var query = new QueryExpression("entres_entityresolutionschematables")
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
                // Nested foreach: iterate all schema records
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

                    foreach (var prop in payload.Properties())
                    {
                        if (string.Equals(prop.Name, fieldName, StringComparison.OrdinalIgnoreCase))
                        {
                            tracing.Trace($"MATCH FOUND: {fieldName} = {prop.Value}");

                            results.Add(new
                            {
                                withCoreSchemaName = fieldName,
                                value = prop.Value.Type == JTokenType.Null ? null : prop.Value.ToObject<object>()
                            });

                            // Once matched, stop inner loop
                            break;
                        }
                    }
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
