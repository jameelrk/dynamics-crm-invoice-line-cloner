using System;
using System.Activities;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Workflow;

namespace DynamicsCrm.InvoiceLineCloner
{
    public sealed class CloneInvoiceLineItems : CodeActivity
    {
        [RequiredArgument]
        [Input("Source Invoice")]
        [ReferenceTarget("invoice")]
        public InArgument<EntityReference> SourceInvoice { get; set; }

        [RequiredArgument]
        [Input("Target Invoice")]
        [ReferenceTarget("invoice")]
        public InArgument<EntityReference> TargetInvoice { get; set; }

        [Input("Continue on line error")]
        [Default("False")]
        public InArgument<bool> ContinueOnLineError { get; set; }

        [Output("Created Line Count")]
        public OutArgument<int> CreatedLineCount { get; set; }

        [Output("Failed Line Count")]
        public OutArgument<int> FailedLineCount { get; set; }

        protected override void Execute(CodeActivityContext executionContext)
        {
            if (executionContext == null) throw new ArgumentNullException("executionContext");

            var tracing = executionContext.GetExtension<ITracingService>();
            var context = executionContext.GetExtension<IWorkflowContext>();
            var factory = executionContext.GetExtension<IOrganizationServiceFactory>();
            if (context == null || factory == null)
                throw new InvalidPluginExecutionException("Required CRM workflow services are unavailable.");

            var source = SourceInvoice.Get(executionContext);
            var target = TargetInvoice.Get(executionContext);
            ValidateInvoiceReference(source, "Source Invoice");
            ValidateInvoiceReference(target, "Target Invoice");
            if (source.Id == target.Id)
                throw new InvalidPluginExecutionException("Source and target invoices must be different records.");

            var service = factory.CreateOrganizationService(context.UserId);
            var continueOnError = ContinueOnLineError.Get(executionContext);
            var created = 0;
            var failed = 0;

            try
            {
                Trace(tracing, "Copying invoice lines from {0} to {1}. Correlation: {2}", source.Id, target.Id, context.CorrelationId);
                var query = new QueryExpression("invoicedetail")
                {
                    ColumnSet = new ColumnSet("productid", "uomid", "quantity", "priceperunit", "description", "isproductoverridden")
                };
                query.Criteria.AddCondition("invoiceid", ConditionOperator.Equal, source.Id);
                query.AddOrder("invoicedetailid", OrderType.Ascending);

                var lines = service.RetrieveMultiple(query);
                foreach (var sourceLine in lines.Entities)
                {
                    try
                    {
                        service.Create(CreateTargetLine(sourceLine, target));
                        created++;
                    }
                    catch (Exception lineException)
                    {
                        failed++;
                        Trace(tracing, "Invoice line {0} failed: {1}", sourceLine.Id, lineException);
                        if (!continueOnError)
                            throw new InvalidPluginExecutionException("An invoice line could not be copied. Review the workflow trace log.", lineException);
                    }
                }

                CreatedLineCount.Set(executionContext, created);
                FailedLineCount.Set(executionContext, failed);
                Trace(tracing, "Invoice line copy completed. Created: {0}; Failed: {1}.", created, failed);
            }
            catch (InvalidPluginExecutionException) { throw; }
            catch (Exception exception)
            {
                Trace(tracing, "Invoice line copy failed: {0}", exception);
                throw new InvalidPluginExecutionException("Invoice line copy failed. Review the workflow trace log.", exception);
            }
        }

        internal static Entity CreateTargetLine(Entity sourceLine, EntityReference targetInvoice)
        {
            var targetLine = new Entity("invoicedetail");
            targetLine["invoiceid"] = targetInvoice;
            CopyIfPresent(sourceLine, targetLine, "productid");
            CopyIfPresent(sourceLine, targetLine, "uomid");
            CopyIfPresent(sourceLine, targetLine, "quantity");
            CopyIfPresent(sourceLine, targetLine, "priceperunit");
            CopyIfPresent(sourceLine, targetLine, "description");
            CopyIfPresent(sourceLine, targetLine, "isproductoverridden");
            targetLine["ispriceoverridden"] = true;
            return targetLine;
        }

        private static void CopyIfPresent(Entity source, Entity target, string attribute)
        {
            if (source.Attributes.Contains(attribute) && source[attribute] != null)
                target[attribute] = source[attribute];
        }

        private static void ValidateInvoiceReference(EntityReference reference, string argumentName)
        {
            if (reference == null || reference.Id == Guid.Empty)
                throw new InvalidPluginExecutionException(argumentName + " is required.");
            if (!string.Equals(reference.LogicalName, "invoice", StringComparison.OrdinalIgnoreCase))
                throw new InvalidPluginExecutionException(argumentName + " must reference an invoice record.");
        }

        private static void Trace(ITracingService tracing, string format, params object[] args)
        {
            if (tracing != null) tracing.Trace(format, args);
        }
    }
}
