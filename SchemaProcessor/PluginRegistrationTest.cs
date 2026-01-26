using Microsoft.Xrm.Sdk;
using System;

namespace MyCompany.Plugins
{
    public class PluginRegistrationTest : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            // Services
            var context = (IPluginExecutionContext)
                serviceProvider.GetService(typeof(IPluginExecutionContext));

            var tracing = (ITracingService)
                serviceProvider.GetService(typeof(ITracingService));

            var serviceFactory = (IOrganizationServiceFactory)
                serviceProvider.GetService(typeof(IOrganizationServiceFactory));

            var service = serviceFactory.CreateOrganizationService(context.UserId);

            tracing.Trace("=== PluginRegistrationTest START ===");

            try
            {
                // Validate target//test
                if (!context.InputParameters.Contains("Target") ||
                    !(context.InputParameters["Target"] is Entity target))
                {
                    tracing.Trace("Target not found or invalid.");
                    return;
                }

                tracing.Trace($"Message: {context.MessageName}");
                tracing.Trace($"Entity: {target.LogicalName}");

                // Only run on Account Update
                if (!string.Equals(context.MessageName, "Update", StringComparison.OrdinalIgnoreCase))
                    return;

                if (target.LogicalName != "account")
                    return;

                // Optional: avoid recursion
                if (context.Depth > 1)
                {
                    tracing.Trace("Depth > 1, exiting to avoid recursion.");
                    return;
                }

                // Read account name if present
                string accountName =
                    target.GetAttributeValue<string>("name") ?? "(name not provided)";

                tracing.Trace($"Account Name: {accountName}");

                // Create a Task record
                var task = new Entity("task");
                task["subject"] = "Plugin Test";
                task["description"] =
                    $"Plugin executed successfully at {DateTime.UtcNow:u}\n" +
                    $"Account: {accountName}";

                Guid taskId = service.Create(task);

                tracing.Trace($"Task created successfully. Id: {taskId}");

                tracing.Trace("=== PluginRegistrationTest END ===");
            }
            catch (Exception ex)
            {
                tracing.Trace("Exception occurred:");
                tracing.Trace(ex.ToString());
                throw; // Important so Dataverse surfaces the error
            }
        }
    }
}
