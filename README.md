# SKF Product Assistant

## Business perspective

The SKF Product Assistant is a business-facing product knowledge service designed to help users find accurate bearing information quickly, without relying on manual catalog searches or specialist intervention. It supports sales teams, customer support teams, and internal product experts by answering structured product questions using the approved product datasheets as the single source of truth.

The solution is built around a simple business workflow:

1. A user asks a product-related question such as width, diameter, or bearing designation.
2. The system identifies the product and attribute being requested.
3. It checks the authoritative local product catalog.
4. It returns a precise answer based on the approved product record.
5. If the answer is missing or not available, it responds transparently instead of guessing.

This reduces support effort, improves answer consistency, and helps business users work with trusted product data rather than unverified assumptions.

## Business problem addressed

Many product teams deal with issues such as:

- repeated requests for basic product specification information
- inconsistent answers across teams and channels
- dependence on manual lookup in large product catalogs
- risk of wrong or inferred values when employees rely on memory or general knowledge

The SKF Product Assistant solves this by providing a consistent and traceable product lookup experience that relies on validated product data only.

## Core business process

The application follows a disciplined business process:

- Receive a request from a user through a single API endpoint.
- Interpret the request and determine whether it is a product question or a feedback item.
- Resolve the exact designation and attribute from the user message.
- Search the local authoritative product database.
- Return only values already contained in the approved data.
- Record any feedback or follow-up context for continuous business improvement.

This keeps the process simple, auditable, and aligned with business rules.

## Business value

The solution delivers value across multiple business scenarios:

- Faster response time for product inquiries
- Better customer experience through accurate product support
- Reduced operational effort for product and sales teams
- Improved trust because the output is grounded in approved data, not generated assumptions
- Better follow-up handling through conversation continuity and feedback capture

## Functional flow

### Product inquiry

A user asks a question such as:

```json
{"message":"What is the width of 6205?"}
```

The platform checks the relevant bearing record, finds the exact product attribute, and returns a grounded answer.

### Follow-up inquiry

A returning user can continue a conversation contextually:

```json
{"conversationId":"returned-id","message":"And what about its diameter?"}
```

This supports a business conversation flow without forcing the user to repeat the full product context each time.

### Feedback capture

If the user provides feedback on the answer, the system stores that feedback for later review and process refinement. This helps improve the service without changing the product data itself.

## Operating model

The assistant is designed as a lightweight operational service:

- It exposes one business-facing HTTP endpoint.
- It uses the approved product catalog as the source of truth.
- It stores short-lived conversation state and feedback in Redis.
- It maintains a correlation ID for request traceability.
- It records operational logs without exposing sensitive values.

This makes the service suitable for internal business use, customer support assist, and product information enablement.

## Prerequisites

To run the solution in a local business environment, the following are required:

- .NET 8 SDK
- Visual Studio 2022 with Azure development support
- Azure Functions runtime support
- Azurite installed locally for storage emulation
- Azure OpenAI access and model deployment
- Redis connection for cache and state management

Install Azurite with:

```powershell
npm install -g azurite
```

## Environment configuration

Set the following environment values before startup:

- `AzureOpenAI__Endpoint`
- `AzureOpenAI__ApiKey`
- `AzureOpenAI__ApiVersion`
- `AzureOpenAI__Deployment`
- `AzureOpenAI__ModelName`
- `Redis__ConnectionString`
- `AzureWebJobsStorage` = `UseDevelopmentStorage=true`
- `FUNCTIONS_WORKER_RUNTIME` = `dotnet-isolated`

Example setup with user secrets:

```powershell
dotnet user-secrets set "AzureOpenAI__Endpoint" "https://<your-resource>.openai.azure.com/"
dotnet user-secrets set "AzureOpenAI__ApiKey" "<your-key>"
dotnet user-secrets set "AzureOpenAI__ApiVersion" "2026-06-01"
dotnet user-secrets set "AzureOpenAI__Deployment" "<your-deployment>"
dotnet user-secrets set "AzureOpenAI__ModelName" "<your-model>"
dotnet user-secrets set "Redis__ConnectionString" "<your-redis-connection-string>"
dotnet user-secrets set "AzureWebJobsStorage" "UseDevelopmentStorage=true"
dotnet user-secrets set "FUNCTIONS_WORKER_RUNTIME" "dotnet-isolated"
```

## Running the solution in Visual Studio

1. Open `SkfProductAssistant.sln` in Visual Studio.
2. Set `SkfProductAssistant.Functions` as the startup project.
3. Select the `SKF Product Assistant` launch profile.
4. Press `F5`.

The app uses the settings in `Properties/launchSettings.json` and automatically starts Azurite when running in local development mode.

The HTTP endpoint is:

```text
http://localhost:7071/api/chat
```

## Governance and quality controls

The service is designed for business trust and operational reliability:

- It uses approved product data rather than general AI knowledge.
- It avoids fabricated values by returning not-found responses when a value is missing.
- It keeps request tracking through correlation IDs and logs.
- It separates business questions from feedback capture for clearer operational handling.
- It protects sensitive configuration values through environment and secret management.

## Summary

From a business perspective, the SKF Product Assistant turns product information access into a reliable, repeatable, and low-effort support process. It reduces manual lookup effort, improves answer accuracy, and gives teams a dependable mechanism to retrieve and validate product details from the official product catalog.