# JbNet — Social Job Referral Network

JbNet lets job seekers find who in their extended professional network works at a target company and send their resume through that chain for an internal referral — turning cold applications into warm introductions.

## Core Concept

You want a job at Infosys. You don't know anyone there directly. But your friend Raj works at TCS, and Raj's connection Priya works at Infosys. JbNet discovers that path and lets you send your resume through it with consent at every hop.

## Architecture

- **Frontend:** Flutter (Android, iOS)
- **Backend:** ASP.NET Core 9 / C# — AWS Lambda (serverless)
- **Database:** DynamoDB (single-table design)
- **Auth:** AWS Cognito (JWT, RBAC)
- **IaC:** AWS CDK (C#)
- **Notifications:** AWS SNS + APNs/FCM
- **Storage:** S3 (resume PDFs)
- **Cloud:** AWS ap-south-1 (Mumbai)

## Project Structure

```
src/
├── Api/                    # Lambda handlers and API Gateway entry points
├── Application/            # Use cases, command/query handlers, DTOs, validators
├── Domain/                 # Entities, value objects, domain services, repository interfaces
├── Infrastructure/         # DynamoDB repos, AWS SDK adapters, external API clients
├── Shared/                 # Cross-cutting: logging, exceptions, extensions, constants
├── Functions/              # Non-API Lambda functions (event consumers, scheduled)
└── Contracts/              # Shared event schemas and API contracts

mobile/                     # Flutter mobile app (Android + iOS)

infrastructure/
└── cdk/                    # AWS CDK stacks (C#)

tests/
├── Unit/                   # Domain and Application layer tests
├── Integration/            # Infrastructure layer tests
└── Api/                    # API contract and end-to-end tests

docs/
└── qa/                     # QA test cases
```

## Development

See [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) for setup instructions.

## License

Private — All rights reserved.
