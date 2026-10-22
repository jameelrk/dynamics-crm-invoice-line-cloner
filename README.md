# Dynamics CRM Invoice Line Cloner

A reusable custom workflow activity for **Microsoft Dynamics CRM 2015** that copies invoice line items (`invoicedetail`) from one invoice to another.

Classic CRM workflows cannot natively iterate through related invoice products and recreate them on another invoice. This assembly adds a workflow step named **Clone Invoice Line Items**.

## Features

- Copies product, unit, quantity, price, description, and override flags
- Supports product-catalog and write-in invoice lines
- Validates that both workflow inputs reference invoice records
- Uses the workflow owner/caller security context
- Traces execution and reports actionable failures
- Fails fast by default to avoid silently reporting a partially copied invoice
- Optional continue-on-error mode with created/failed output counts
- Contains no credentials, connection strings, environment URLs, binaries, or signing keys

## Compatibility

- Microsoft Dynamics CRM 2015 / SDK 7.x
- .NET Framework 4.5.2
- Visual Studio 2015 or later with the .NET Framework 4.5.2 targeting pack

> This is a legacy CRM 2015 extension. Test it in a non-production organization before deployment.

## Build

```powershell
nuget restore DynamicsCrm.InvoiceLineCloner.sln
msbuild DynamicsCrm.InvoiceLineCloner.sln /p:Configuration=Release
```

The compiled assembly is created under `src\DynamicsCrm.InvoiceLineCloner\bin\Release`.

### Strong-name signing

No `.snk` file is committed because a signing key is private material. If your deployment requires strong naming, create your own key locally:

```powershell
sn -k src\DynamicsCrm.InvoiceLineCloner\InvoiceLineCloner.snk
msbuild DynamicsCrm.InvoiceLineCloner.sln /p:Configuration=Release /p:SignAssembly=true /p:AssemblyOriginatorKeyFile=InvoiceLineCloner.snk
```

The repository ignores `*.snk`, so the private key will not be committed accidentally.

## Register and use

1. Build the Release configuration.
2. Register `DynamicsCrm.InvoiceLineCloner.dll` using the CRM Plugin Registration Tool.
3. Create or edit a classic workflow.
4. Add the **Clone Invoice Line Items** custom workflow step.
5. Supply the source invoice and target invoice references.
6. Leave **Continue on line error** set to `No` unless partial completion is explicitly acceptable.
7. Run and verify the workflow in a test organization.

The workflow identity needs read access to the source invoice products and create/append/append-to access for target invoice products and invoices.

## Inputs and outputs

- **Source Invoice**: invoice whose line items will be copied
- **Target Invoice**: invoice that receives the copied lines
- **Continue on line error**: if enabled, traces a failed line and continues
- **Created Line Count**: number of lines created
- **Failed Line Count**: number of lines that failed

## Important behavior

The activity does not copy calculated totals, tax, discounts, or custom attributes. CRM recalculates system-calculated values. Extend `InvoiceLineCloner.cs` deliberately if your organization requires custom invoice-detail fields.

The operation is not inherently idempotent. Running it twice creates duplicate target lines. Design the calling workflow to execute once or add an organization-specific duplicate marker.

## Security

This project uses no external endpoint and stores no credentials. Before publishing a fork, review the complete Git history and enable GitHub secret scanning/push protection where available.

## License

MIT. See [LICENSE](LICENSE).
