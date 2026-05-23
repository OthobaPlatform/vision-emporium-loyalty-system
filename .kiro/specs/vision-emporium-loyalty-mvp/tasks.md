# Implementation Plan: Vision Emporium Loyalty System MVP

## Overview

This plan implements the Vision Emporium Loyalty System MVP as an AWS serverless application using .NET 8 Native AOT Lambdas, DynamoDB single-table design, React SPA frontend, and custom JWT authentication (Auth_Lambda with DynamoDB user store and HMAC-SHA256 signed tokens). Tasks are ordered to build foundational layers first (data models, shared libraries), then core business logic (ingestion, eligibility, redemption), followed by the frontend and integration wiring.

## Tasks

- [x] 1. Set up project structure and shared libraries
  - [x] 1.1 Create solution structure and core projects
    - Create .NET solution with projects: `VELoyalty.Core`, `VELoyalty.Data`, `VELoyalty.Auth`, `VELoyalty.Notifications`, `VELoyalty.Api`, `VELoyalty.SyncJob`, `VELoyalty.ExcelProcessor`, `VELoyalty.StreamProcessor`, `VELoyalty.NotificationHandler`, `VELoyalty.AuthLambda`, `VELoyalty.Authorizer`
    - Create test projects: `VELoyalty.Core.Tests`, `VELoyalty.Data.Tests`, `VELoyalty.Api.Tests`, `VELoyalty.Auth.Tests`
    - Add NuGet references: AWSSDK.DynamoDBv2, AWSSDK.SecretsManager, FsCheck.Xunit, xUnit, Moq, ClosedXML (for Excel), Amazon.Lambda.AspNetCoreServer.Hosting, BCrypt.Net-Next, System.IdentityModel.Tokens.Jwt
    - Configure Native AOT publishing profiles for all Lambda projects
    - _Requirements: 10.1, 10.2_

  - [x] 1.2 Define domain models and constants in VELoyalty.Core
    - Implement all domain records: `Customer`, `Purchase`, `LoyaltyCycle`, `PurchaseThreshold`, `VerificationCode`, `Redemption`, `Outlet`, `SyncJobResult`, `AuditEntry`, `ImportJobResult`, `NotificationLog`, `User`, `AuthToken`
    - Define constants for system currency (BDT), time zone (Asia/Dhaka), phone number defaults (+880)
    - Define enums: `GiftType`, `CodeStatus`, `JobStatus`, `UserRole`, `AuditEventType`
    - _Requirements: 10.1, 3.1, 4.1, 5.1_

  - [x] 1.3 Implement validation logic in VELoyalty.Core
    - Implement `TransactionValidator` for required field presence, amount range (0.01–999,999,999.99), date parsing
    - Implement `PhoneNumberValidator` for E.164 format with +880 default
    - Implement `CycleValidator` for date ordering and duration (30–730 days)
    - Implement `ThresholdValidator` for range (1–100), count (1–10), uniqueness
    - Implement `ExcelSchemaValidator` for column presence and type constraints
    - _Requirements: 1.2, 1.8, 2.1, 3.1, 3.4, 4.1, 4.9, 12.6_

  - [ ]* 1.4 Write property tests for transaction validation
    - **Property 1: Transaction Record Validation**
    - **Validates: Requirements 1.2, 1.8**

  - [ ]* 1.5 Write property tests for deduplication logic
    - **Property 2: Deduplication by Composite Key**
    - **Validates: Requirements 1.5, 2.8**

  - [ ]* 1.6 Write property tests for configuration validators
    - **Property 4: Sync Interval Validation**
    - **Property 6: Loyalty Cycle Date Validation**
    - **Property 11: Threshold Configuration Validation**
    - **Validates: Requirements 1.6, 3.1, 3.4, 4.1, 4.9**

  - [ ]* 1.7 Write property tests for Excel schema validation
    - **Property 5: Excel Schema Validation**
    - **Validates: Requirements 2.1, 2.3**

- [x] 2. Implement DynamoDB data layer (VELoyalty.Data)
  - [x] 2.1 Implement DynamoDB repository base and table configuration
    - Create `DynamoDbContext` with table name, GSI definitions, and serialization helpers
    - Implement composite key builders for all entity types (PK/SK/GSI patterns from design)
    - Implement generic `PutItem`, `GetItem`, `Query`, `BatchWrite` operations with retry handling
    - _Requirements: 10.1_

  - [x] 2.2 Implement Customer and Purchase repositories
    - Implement `CustomerRepository`: create/update customer profile, get by ID, get by phone (GSI1)
    - Implement `PurchaseRepository`: store purchase, query by customer+cycle, check duplicates by composite key
    - Implement qualifying purchase count calculation (filter by min amount and excluded categories)
    - _Requirements: 6.1, 6.2, 4.7, 4.8, 1.5_

  - [x] 2.3 Implement Configuration and Cycle repositories
    - Implement `ConfigRepository`: get/put cycle config, get/put threshold configs, get/put general config
    - Implement `CycleRepository`: get active cycle, archive cycle data, reset purchase counts
    - _Requirements: 3.1, 3.2, 3.3, 4.1_

  - [x] 2.4 Implement Eligibility, Verification Code, and Redemption repositories
    - Implement `EligibilityRepository`: create eligibility record, check existing eligibility by customer+cycle+tier
    - Implement `VerificationCodeRepository`: generate and store 6-digit code, lookup by code (GSI2), update status
    - Implement `RedemptionRepository`: create redemption record, check if code already redeemed
    - Implement rate limit tracking: increment attempts, check/set block status with TTL
    - _Requirements: 5.1, 5.2, 5.3, 5.6, 5.7, 5.11_

  - [x] 2.5 Implement Outlet, User, Audit, and Job repositories
    - Implement `OutletRepository`: CRUD operations, active/inactive status, count active outlets
    - Implement `UserRepository`: CRUD operations, lookup by email (GSI1: `GSI1_USER` / `USER#{email}`), store bcrypt password hash
    - Implement `AuditRepository`: append-only write, query by time range and event type
    - Implement `SyncJobRepository` and `ImportJobRepository`: create/update job records, query by status
    - _Requirements: 8.1, 8.2, 9.1, 9.2, 9.3, 9.5, 1.7, 2.4, 7.4, 7.9_

  - [ ]* 2.6 Write property tests for eligibility determination
    - **Property 12: Eligibility Determination**
    - **Property 14: Qualifying Purchase Filter**
    - **Validates: Requirements 4.2, 4.6, 4.7, 4.8**

  - [ ]* 2.7 Write property tests for cycle operations
    - **Property 7: Cycle Reset Zeroes All Counts**
    - **Property 8: Cycle Data Archival Preservation**
    - **Property 9: Cycle Modification Isolation**
    - **Property 10: Days Remaining Calculation**
    - **Validates: Requirements 3.2, 3.3, 3.5, 3.6**

- [x] 3. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Implement API sync and data ingestion
  - [x] 4.1 Implement Sync Job Lambda
    - Implement external API client with configurable endpoint and credentials from DynamoDB config
    - Implement retry logic with exponential backoff (5s, 10s, 20s) and 30-second timeout
    - Implement record validation, deduplication, and batch write to DynamoDB
    - Record sync job result (success/partial/failed) with counts
    - Wire EventBridge Scheduler trigger with configurable interval (min 15 min)
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.7, 1.8_

  - [x] 4.2 Implement Excel Processor Lambda
    - Implement S3 event trigger handler for uploaded .xlsx files
    - Implement file size validation (max 10MB) and row count validation (max 100,000)
    - Parse Excel using ClosedXML, validate each row against schema
    - Implement deduplication check against existing records
    - Write valid records to DynamoDB, generate import summary with rejected row details
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.8_

  - [x] 4.3 Implement Excel upload API endpoint and template download
    - Implement `POST /api/v1/ingestion/upload`: validate file format/size, upload to S3, return job ID
    - Implement `GET /api/v1/ingestion/jobs/{id}`: return import job status and summary
    - Implement `GET /api/v1/ingestion/template`: return downloadable Excel template with headers and sample data
    - _Requirements: 2.2, 2.4, 2.7_

  - [ ]* 4.4 Write property tests for ingestion summary accuracy
    - **Property 3: Ingestion Summary Accuracy**
    - **Validates: Requirements 1.7, 2.4**

- [x] 5. Implement eligibility evaluation and notifications
  - [x] 5.1 Implement Stream Processor Lambda
    - Configure DynamoDB Streams trigger for INSERT events on purchase records
    - Evaluate customer's qualifying purchase count against enabled thresholds
    - Apply minimum purchase amount and excluded category filters
    - Create eligibility record and generate verification code bound to most recent qualifying purchase outlet
    - Invoke Notification Lambda when threshold is reached
    - _Requirements: 4.2, 4.6, 4.7, 4.8, 5.1, 5.2_

  - [x] 5.2 Implement Notification Lambda
    - Implement SMS gateway client abstraction in VELoyalty.Notifications
    - Compose eligibility SMS with customer name, gift description, outlet name, and verification code
    - Compose reminder SMS for codes within 7 days of expiration
    - Implement retry logic (3 attempts, 1-hour intervals)
    - Validate phone number before sending; log undeliverable if invalid
    - Record notification log with delivery status, recipient, and timestamp
    - _Requirements: 5.1, 12.1, 12.2, 12.3, 12.4, 12.5, 12.6_

  - [ ]* 5.3 Write property tests for verification code and notifications
    - **Property 15: Verification Code Outlet Binding**
    - **Property 28: Notification Content Completeness**
    - **Property 29: Expiry Reminder Triggering**
    - **Property 30: Phone Number Validation for Notifications**
    - **Validates: Requirements 5.2, 12.1, 12.2, 12.3, 12.6**

- [x] 6. Implement gift redemption service
  - [x] 6.1 Implement redemption verification logic
    - Implement `POST /api/v1/redemptions/verify`: validate code format (6-digit numeric), check existence, check expiry, check outlet binding, check rate limit, check already redeemed
    - On success: mark code as redeemed, record redemption with timestamp/outlet/staff, create audit entry
    - On failure: return appropriate error (invalid, expired, wrong outlet, already redeemed, rate limited)
    - Implement rate limiting: track failed attempts per code, block after 5 failures in 15 minutes for 30 minutes
    - _Requirements: 5.3, 5.4, 5.5, 5.6, 5.7, 5.8, 5.10, 5.11_

  - [x] 6.2 Implement redemption search and customer lookup endpoints
    - Implement `GET /api/v1/redemptions/search`: search by phone number or verification code
    - Display customer name, gift tier, code status (active/redeemed/expired), designated outlet
    - Implement `GET /api/v1/customers/{phone}`: return full customer profile with progress
    - Implement `GET /api/v1/customers/{phone}/codes`: return all verification codes for current cycle
    - _Requirements: 5.9, 6.1, 6.2, 6.3, 6.4, 6.5, 6.6_

  - [ ]* 6.3 Write property tests for redemption validation
    - **Property 16: Redemption Validation**
    - **Property 17: One-Time Redemption (Idempotence)**
    - **Property 18: Code Expiration Calculation**
    - **Property 19: Rate Limiting Enforcement**
    - **Validates: Requirements 5.3, 5.4, 5.5, 5.6, 5.7, 5.8, 5.10, 5.11**

  - [ ]* 6.4 Write property tests for customer profile
    - **Property 20: Customer Profile Progress Calculation**
    - **Validates: Requirements 6.1, 6.4, 6.5**

- [ ] 7. Implement authentication, authorization, and outlet management
  - [x] 7.1 Implement Auth_Lambda (login endpoint with JWT issuance)
    - Implement `POST /api/v1/auth/login` as a standalone Lambda function (public endpoint, no authorizer)
    - Accept JSON body with `email` and `password` fields
    - Look up user record in DynamoDB by email (GSI1: `GSI1_USER` / `USER#{email}`)
    - Verify password against stored bcrypt hash (cost factor 12) using BCrypt.Net
    - On success: generate JWT with claims (sub=userId, role, outletId if Outlet_Manager, iat, exp with 8-hour default expiry)
    - Sign JWT using HMAC-SHA256 with secret retrieved from AWS Secrets Manager
    - Return signed token in JSON response; return HTTP 401 for invalid credentials
    - _Requirements: 7.6, 7.8, 7.9_

  - [x] 7.2 Implement Custom Lambda Authorizer
    - Implement token-based Lambda authorizer for API Gateway
    - Extract token from `Authorization: Bearer {token}` header
    - Verify HMAC-SHA256 signature using shared secret from AWS Secrets Manager
    - Check token expiry (`exp` claim vs current time)
    - On valid token: return IAM Allow policy with principal context containing userId, role, outletId
    - On invalid/expired/missing token: return IAM Deny policy (results in HTTP 401)
    - _Requirements: 7.7_

  - [x] 7.3 Implement role-based authorization middleware (VELoyalty.Auth)
    - Extract `role` and `outletId` from custom JWT claims passed via API Gateway request context
    - Implement policy-based authorization attributes for Admin and Outlet_Manager roles
    - Implement outlet-scoped data filtering for Outlet_Manager role (restrict to assigned outletId)
    - Return HTTP 403 for insufficient permissions
    - _Requirements: 7.1, 7.2, 7.3, 7.5_

  - [x] 7.4 Implement user management endpoints
    - Implement `GET /api/v1/users`: list all users (Admin only)
    - Implement `POST /api/v1/users`: create user in DynamoDB with bcrypt password hash (cost factor 12) and role assignment (Admin or Outlet_Manager with outletId)
    - Implement `PUT /api/v1/users/{id}`: update user details, role, and optionally reset password
    - _Requirements: 7.4_

  - [x] 7.5 Implement outlet management endpoints
    - Implement `GET /api/v1/outlets`: list all outlets with status
    - Implement `POST /api/v1/outlets`: create new outlet
    - Implement `PUT /api/v1/outlets/{id}`: update outlet details
    - Implement `PATCH /api/v1/outlets/{id}/status`: activate/deactivate with last-active-outlet protection
    - Block redemptions at deactivated outlets
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5_

  - [ ]* 7.6 Write property tests for authorization and outlet rules
    - **Property 21: Authorization Enforcement**
    - **Property 22: JWT Token Validation**
    - **Property 23: Deactivated Outlet Blocks Redemption**
    - **Property 24: Last Active Outlet Protection**
    - **Validates: Requirements 7.2, 7.3, 7.5, 7.6, 7.7, 8.3, 8.4**

- [ ] 8. Implement configuration and audit endpoints
  - [x] 8.1 Implement configuration management endpoints
    - Implement `GET /api/v1/config/cycle` and `PUT /api/v1/config/cycle`: loyalty cycle CRUD with validation
    - Implement `GET /api/v1/config/thresholds` and `PUT /api/v1/config/thresholds`: threshold CRUD with validation
    - Implement `GET /api/v1/config/general` and `PUT /api/v1/config/general`: general settings (sync interval, code expiry days, min purchase amount, excluded categories)
    - Apply cycle changes to next cycle only (not current active)
    - Apply threshold changes to future purchases only (non-retroactive)
    - Record all configuration changes in audit log
    - _Requirements: 3.1, 3.4, 3.5, 4.1, 4.3, 4.4, 4.5, 4.9, 9.2_

  - [x] 8.2 Implement audit log and dashboard endpoints
    - Implement `GET /api/v1/audit`: query audit records with time range and event type filters
    - Implement `GET /api/v1/dashboard`: return summary (active customers, pending redemptions, cycle status with days remaining, recent sync status)
    - Implement `POST /api/v1/ingestion/sync`: trigger manual sync job
    - Implement `GET /api/v1/ingestion/sync/status`: return sync job history
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 3.6, 1.6, 1.7_

  - [ ]* 8.3 Write property tests for audit and threshold changes
    - **Property 25: Audit Record Completeness**
    - **Property 13: Non-Retroactive Threshold Changes**
    - **Validates: Requirements 9.1, 9.2, 9.3, 9.5, 4.4**

- [x] 9. Checkpoint - Ensure all backend tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 10. Implement React frontend
  - [x] 10.1 Set up React project with routing and custom authentication
    - Initialize React project with TypeScript, Vite, and TailwindCSS
    - Implement custom Auth_Login_Page with email/password form
    - Authenticate via `POST /api/v1/auth/login` endpoint (Auth_Lambda)
    - Store returned JWT in localStorage; attach as `Authorization: Bearer {token}` header on all API requests
    - Implement token expiry check and redirect to login on 401 responses
    - Implement protected route wrapper that checks for valid JWT presence
    - Implement role-based routing: Admin → Dashboard, Outlet_Manager → Redemptions
    - Implement unauthorized access redirect with permission message
    - _Requirements: 11.1, 11.4, 11.7, 7.6_

  - [x] 10.2 Implement layout, navigation, and shared components
    - Implement responsive layout (desktop ≥1024px, tablet 768–1023px)
    - Implement role-based navigation menu (Admin: Dashboard, Customers, Redemptions, Configuration, Outlets, Users; Outlet_Manager: Redemptions, Customers)
    - Implement shared components: loading indicator (300ms threshold), success/error toast notifications, data tables with pagination
    - _Requirements: 11.2, 11.3, 11.5_

  - [x] 10.3 Implement Redemption and Customer Lookup pages
    - Implement redemption verification form: code input with 6-digit validation, submit, display result (success/error with specific messages)
    - Implement customer search by phone number: display profile, purchase progress, verification codes
    - Implement redemption search: search by phone or code, display results
    - Implement client-side form validation with inline error messages
    - _Requirements: 5.9, 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 11.6_

  - [x] 10.4 Implement Configuration pages (Admin)
    - Implement Loyalty Cycle configuration form with date pickers and validation (30–730 days)
    - Implement Purchase Thresholds management: add/edit/enable/disable tiers with gift type, description, value
    - Implement General Settings: sync interval, code expiry, min purchase amount, excluded categories
    - Implement client-side validation for all configuration forms
    - _Requirements: 3.1, 3.4, 4.1, 4.3, 4.5, 4.9, 11.6_

  - [x] 10.5 Implement Dashboard, Outlet Management, User Management, and Import pages
    - Implement Admin Dashboard: cycle status with days remaining, active customers count, pending redemptions, recent sync status
    - Implement Outlet Management: list, create, edit, activate/deactivate with last-outlet protection message
    - Implement User Management: list, create, edit users with role assignment (Admin or Outlet_Manager with outlet selection)
    - Implement Import page: file upload with drag-and-drop, template download, job status display with summary
    - Implement Sync Status page: job history, manual sync trigger
    - _Requirements: 3.6, 8.1, 8.2, 8.4, 8.5, 7.4, 2.2, 2.4, 2.7, 1.7_

  - [ ]* 10.6 Write property tests for frontend role rendering and form validation
    - **Property 26: Role-Based Menu Rendering**
    - **Property 27: Client-Side Form Validation**
    - **Validates: Requirements 11.3, 11.6**

- [ ] 11. Infrastructure and deployment configuration
  - [x] 11.1 Create AWS CDK or SAM infrastructure-as-code
    - Define DynamoDB table with GSIs, on-demand billing, and stream enabled
    - Define Lambda functions with Native AOT runtime, 512MB memory, 30s timeout (API Handler, Sync Job, Excel Processor, Stream Processor, Notification Handler, Auth Lambda, Custom Lambda Authorizer)
    - Define API Gateway HTTP API with Custom Lambda Authorizer (token-based, validates JWT signature)
    - Define Auth_Lambda as a separate public endpoint (no authorizer) for `POST /api/v1/auth/login`
    - Define AWS Secrets Manager secret for JWT signing key (HMAC-SHA256 shared secret)
    - Define S3 buckets (frontend hosting, file uploads) with appropriate policies
    - Define CloudFront distribution with S3 origin and API Gateway origin
    - Define EventBridge Scheduler for sync job
    - Configure all resources in ap-south-1 region
    - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5, 10.6, 10.7_

  - [x] 11.2 Configure API Gateway routes and Lambda integrations
    - Map all API endpoints to Lambda API handler with path-based routing
    - Map `POST /api/v1/auth/login` to Auth_Lambda (no authorizer attached)
    - Attach Custom Lambda Authorizer to all other API routes
    - Configure S3 event notification for Excel processor Lambda
    - Configure DynamoDB Streams trigger for Stream Processor Lambda
    - Set up URL path versioning (/api/v1/) on API Gateway
    - _Requirements: 10.6_

- [x] 12. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP delivery
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation of the system
- Property tests use FsCheck for .NET and validate universal correctness properties from the design
- Unit tests use xUnit with Moq and validate specific examples and edge cases
- The system uses Asia/Dhaka timezone for display and UTC for storage
- All monetary values are in BDT with 2 decimal places
- Phone numbers are normalized to E.164 format with +880 default
- Authentication uses custom Auth_Lambda with DynamoDB user store (no AWS Cognito)
- JWT tokens are signed with HMAC-SHA256 using a secret stored in AWS Secrets Manager
- Passwords are hashed with bcrypt (cost factor 12) before storage in DynamoDB

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["1.2"] },
    { "id": 2, "tasks": ["1.3", "2.1"] },
    { "id": 3, "tasks": ["1.4", "1.5", "1.6", "1.7", "2.2", "2.3", "2.4", "2.5"] },
    { "id": 4, "tasks": ["2.6", "2.7", "4.1", "4.2"] },
    { "id": 5, "tasks": ["4.3", "4.4", "5.1"] },
    { "id": 6, "tasks": ["5.2", "6.1"] },
    { "id": 7, "tasks": ["5.3", "6.2", "7.1", "7.2"] },
    { "id": 8, "tasks": ["6.3", "6.4", "7.3"] },
    { "id": 9, "tasks": ["7.4", "7.5", "7.6"] },
    { "id": 10, "tasks": ["8.1"] },
    { "id": 11, "tasks": ["8.2", "8.3"] },
    { "id": 12, "tasks": ["10.1", "11.1"] },
    { "id": 13, "tasks": ["10.2", "11.2"] },
    { "id": 14, "tasks": ["10.3", "10.4"] },
    { "id": 15, "tasks": ["10.5"] },
    { "id": 16, "tasks": ["10.6"] }
  ]
}
```
