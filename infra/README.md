# Vision Emporium Loyalty System - Infrastructure

AWS SAM (Serverless Application Model) infrastructure-as-code for the Vision Emporium Loyalty System MVP.

## Architecture

All resources are deployed in **ap-south-1 (Mumbai)** region.

### Resources

| Resource | Type | Description |
|----------|------|-------------|
| VELoyaltyTable | DynamoDB | Single-table design with GSI1, GSI2, streams enabled |
| VELoyaltyApi | Lambda (.NET 8 AOT) | Main API handler for all REST endpoints |
| VELoyaltyAuthLambda | Lambda (.NET 8 AOT) | Authentication - issues JWT tokens (public endpoint) |
| VELoyaltyAuthorizer | Lambda (.NET 8 AOT) | Custom Lambda Authorizer - validates JWT tokens |
| VELoyaltySyncJob | Lambda (.NET 8 AOT) | Scheduled sync from external sales API |
| VELoyaltyExcelProcessor | Lambda (.NET 8 AOT) | Processes uploaded Excel files from S3 |
| VELoyaltyStreamProcessor | Lambda (.NET 8 AOT) | Evaluates eligibility from DynamoDB stream events |
| VELoyaltyNotificationHandler | Lambda (.NET 8 AOT) | Sends SMS notifications via gateway |
| VELoyaltyHttpApi | API Gateway HTTP API | REST API with Custom Lambda Authorizer |
| FrontendBucket | S3 | React SPA hosting |
| UploadsBucket | S3 | Excel file uploads |
| CloudFrontDistribution | CloudFront | CDN with S3 + API Gateway origins |
| JwtSigningSecret | Secrets Manager | HMAC-SHA256 signing key for JWT |
| EventBridge Schedules | Scheduler | Sync job + expiry reminder triggers |

### Lambda Configuration

All Lambda functions use:
- Runtime: `provided.al2023` (Native AOT custom runtime)
- Memory: 512 MB
- Timeout: 30 seconds
- Architecture: x86_64

## Prerequisites

- [AWS SAM CLI](https://docs.aws.amazon.com/serverless-application-model/latest/developerguide/install-sam-cli.html)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- AWS credentials configured for ap-south-1

## Build & Deploy

### Build all Lambda functions

```bash
# From the repository root, publish each Lambda with Native AOT
dotnet publish src/VELoyalty.Api -c Release -r linux-x64 -o src/VELoyalty.Api/publish
dotnet publish src/VELoyalty.AuthLambda -c Release -r linux-x64 -o src/VELoyalty.AuthLambda/publish
dotnet publish src/VELoyalty.Authorizer -c Release -r linux-x64 -o src/VELoyalty.Authorizer/publish
dotnet publish src/VELoyalty.SyncJob -c Release -r linux-x64 -o src/VELoyalty.SyncJob/publish
dotnet publish src/VELoyalty.ExcelProcessor -c Release -r linux-x64 -o src/VELoyalty.ExcelProcessor/publish
dotnet publish src/VELoyalty.StreamProcessor -c Release -r linux-x64 -o src/VELoyalty.StreamProcessor/publish
dotnet publish src/VELoyalty.NotificationHandler -c Release -r linux-x64 -o src/VELoyalty.NotificationHandler/publish
```

### Deploy with SAM

```bash
cd infra

# Validate template
sam validate

# Deploy to production
sam deploy

# Deploy to dev environment
sam deploy --config-env dev
```

### Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| Environment | prod | Deployment environment (dev/staging/prod) |
| SyncIntervalMinutes | 60 | Sync job interval (min: 15 minutes) |
| FrontendBucketName | ve-loyalty-frontend | S3 bucket name for frontend |
| UploadsBucketName | ve-loyalty-uploads | S3 bucket name for uploads |

## API Gateway Routes

| Method | Path | Auth | Lambda |
|--------|------|------|--------|
| POST | /api/v1/auth/login | None (public) | VELoyaltyAuthLambda |
| ANY | /api/v1/{proxy+} | Custom Lambda Authorizer | VELoyaltyApi |

## CloudFront Routing

| Path Pattern | Origin | Caching |
|--------------|--------|---------|
| /api/* | API Gateway | Disabled |
| /* (default) | S3 Frontend | Optimized |

SPA client-side routing is supported via custom error responses (403/404 → index.html).
