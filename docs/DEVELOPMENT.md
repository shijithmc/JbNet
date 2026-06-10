# Development Setup

## Prerequisites

- .NET 9 SDK
- Flutter SDK 3.x
- AWS CLI (configured with dev credentials)
- AWS CDK CLI (`npm install -g aws-cdk`)
- Docker (for DynamoDB Local)
- `gh` CLI (for GitHub operations)

## Local Development

### Backend (.NET)
```bash
cd src
dotnet restore
dotnet build
```

### Run DynamoDB Local
```bash
docker run -p 8000:8000 amazon/dynamodb-local
```

### Flutter Mobile
```bash
cd mobile
flutter pub get
flutter run
```

### Infrastructure (CDK)
```bash
cd infrastructure/cdk
dotnet restore
cdk synth
cdk deploy --profile dev
```

## Environment Variables

Copy `appsettings.Development.json.example` to `appsettings.Development.json` and fill in values.
Never commit real credentials.

## Branch Convention

- `feat/<description>` — new feature
- `fix/<description>` — bug fix
- `chore/<description>` — tooling, infra, dependencies

All changes via PR to `main`. No direct pushes.
