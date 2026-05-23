# Requirements Document

## Introduction

The Vision Emporium Loyalty System MVP is the minimum viable release of a customer loyalty and rewards platform for Vision Emporium, an electronic goods retail chain under the Pran RFL Group in Bangladesh. This MVP focuses on the essential capabilities needed to deliver immediate business value: ingesting purchase data, tracking customer loyalty progress against configurable thresholds within a loyalty cycle, awarding gifts with SMS-verified outlet-specific redemption, and providing basic role-based access through a minimal web interface. The system is deployed on AWS serverless infrastructure (DynamoDB, Lambda, S3/CloudFront) in ap-south-1 with .NET 8 Native AOT for cost efficiency.

Features deferred to subsequent phases include advanced analytics dashboards, complex report generation, gift catalog management, customer self-service portal, disaster recovery / multi-region replication, load testing / observability guardrails, complex exchange/return workflows, customer identity resolution / merge workflows, advanced accessibility compliance, and multi-language support.

## Glossary

- **Loyalty_System**: The core application managing customer loyalty tracking, gift eligibility, and redemption workflows
- **Data_Ingestion_Service**: The component responsible for importing sales and customer data from an external API and Excel file uploads
- **Loyalty_Cycle**: A configurable time period (default: June 1 to next year May 31) during which customer purchases are tracked for loyalty rewards; dates are interpreted in System_Time_Zone
- **Purchase_Threshold**: A configurable number of purchases (e.g., 3rd, 6th) that triggers gift eligibility for a customer
- **Gift_Redemption_Service**: The component that handles outlet-specific verification and one-time gift claim processing
- **SMS_Verification**: A message sent to eligible customers containing a unique 6-digit numeric verification code tied to a specific outlet
- **Outlet**: A physical Vision Emporium retail store location
- **RBAC_Service**: The role-based access control component using JWT tokens issued by the Auth_Lambda and role assignments stored in DynamoDB user records
- **Auth_Lambda**: The Lambda function responsible for authenticating users against credentials stored in DynamoDB and issuing signed JWT tokens containing user role claims
- **Auth_Login_Page**: A custom login page served by the React frontend that collects user credentials and authenticates via the Auth_Lambda endpoint
- **Configuration_Service**: The component managing configurable parameters including cycles, thresholds, and gift definitions
- **Customer**: A person who makes purchases at Vision Emporium outlets
- **Eligible_Customer**: A customer who has reached a configured Purchase_Threshold within the current Loyalty_Cycle
- **Notification_Service**: The component responsible for sending SMS notifications to customers via a third-party SMS gateway
- **Audit_Service**: The component that records system actions for traceability
- **Customer_Profile**: The aggregated view of a customer's purchase history and loyalty status within the current Loyalty_Cycle
- **Outlet_Manager**: A user role responsible for managing a specific outlet's redemption operations
- **Admin**: A user role with full system configuration and management access
- **Sync_Job**: A scheduled or on-demand data synchronization task between the external sales system and the Loyalty_System
- **Gift_Type**: A classification of a gift associated with a Purchase_Threshold tier; permitted values are Cash_Return and Gift_Item
- **Cash_Return**: A Gift_Type dispensed as a monetary amount given to the customer at the outlet (cash from till)
- **Gift_Item**: A Gift_Type dispensed as a physical product given to the customer at the outlet
- **System_Currency**: Bangladeshi Taka (BDT, ISO 4217); all monetary amounts stored as decimals with two fractional digits
- **System_Time_Zone**: Asia/Dhaka (UTC+06:00); all persisted timestamps stored in ISO 8601 UTC and presented in System_Time_Zone in the UI
- **Valid_Phone_Number**: A phone number conforming to E.164 format with country code, defaulting to Bangladesh (+880) when no country code is supplied; normalised to E.164 before persistence

## Requirements

### Requirement 1: Data Ingestion via External API

**User Story:** As an administrator, I want to ingest sales and customer data from an external API, so that the Loyalty_System has up-to-date purchase records without manual entry.

#### Acceptance Criteria

1. WHEN an external API endpoint is configured, THE Data_Ingestion_Service SHALL fetch sales transaction data including customer identifier, customer name, customer phone number, outlet identifier, purchase date, purchase amount, and product category
2. WHEN the Data_Ingestion_Service receives an API response with HTTP status 200 and a parseable response body, THE Data_Ingestion_Service SHALL validate each transaction record for the presence of all required fields (customer identifier, customer phone number, outlet identifier, purchase date, and purchase amount) and store valid records in DynamoDB with a processing timestamp
3. IF the external API returns an HTTP error status (4xx or 5xx) or is unreachable within 30 seconds, THEN THE Data_Ingestion_Service SHALL log the failure with timestamp and error details and retry up to 3 attempts with exponential backoff starting at 5 seconds
4. IF all retry attempts are exhausted without a successful response, THEN THE Data_Ingestion_Service SHALL mark the Sync_Job as failed and record the failure in the ingestion log
5. WHEN duplicate transaction records are detected based on a matching combination of customer identifier, outlet identifier, purchase date, and purchase amount, THE Data_Ingestion_Service SHALL skip duplicate records and log the occurrence
6. THE Configuration_Service SHALL allow administrators to schedule Sync_Job execution at configurable intervals with a minimum interval of 15 minutes (default: 60 minutes)
7. WHEN a Sync_Job completes, THE Data_Ingestion_Service SHALL record the sync status (success, partial, or failed), records fetched, records stored, and records skipped in an ingestion log
8. IF a transaction record is missing one or more required fields or contains invalid data (non-numeric purchase amount, purchase amount outside 0.01 to 999,999,999.99, or unparseable purchase date), THEN THE Data_Ingestion_Service SHALL reject that record, continue processing remaining records, and log the rejected record with the reason for rejection

### Requirement 2: Data Ingestion via Excel File Import

**User Story:** As an administrator, I want to import sales and customer data from Excel files, so that I can bulk-load historical or offline transaction data into the Loyalty_System.

#### Acceptance Criteria

1. WHEN an administrator uploads an Excel file, THE Data_Ingestion_Service SHALL validate the file format against the expected schema requiring the following columns: customer identifier (non-empty string), customer name (non-empty string, maximum 200 characters), customer phone number (Valid_Phone_Number format), outlet identifier (matching an existing outlet), purchase date (ISO 8601 date format, not in the future), purchase amount (numeric value between 0.01 and 999,999,999.99), and product category (non-empty string)
2. WHEN the Excel file passes schema validation, THE Data_Ingestion_Service SHALL process the upload asynchronously (upload to S3, enqueue a processing job, return a job identifier) and parse and store all valid transaction records in DynamoDB
3. IF the Excel file contains rows with missing or invalid data, THEN THE Data_Ingestion_Service SHALL reject those rows, continue processing valid rows, and return a summary listing each rejected row with its row number and the specific rejection reason
4. WHEN an Excel import completes, THE Data_Ingestion_Service SHALL display the total records processed, records imported, records rejected, and records skipped as duplicates on the import status screen
5. THE Data_Ingestion_Service SHALL support Excel files in .xlsx format up to 10MB in size and up to 100,000 rows
6. WHEN an Excel file exceeds the size or row limit, THE Data_Ingestion_Service SHALL reject the file before processing and inform the administrator of the applicable limits
7. THE Data_Ingestion_Service SHALL provide a downloadable Excel template with the required column headers and sample data
8. WHEN duplicate transaction records are detected during Excel import (matching combination of customer identifier, outlet identifier, purchase date, and purchase amount), THE Data_Ingestion_Service SHALL skip duplicate records and include the count of skipped duplicates in the import summary

### Requirement 3: Configurable Loyalty Cycle

**User Story:** As an administrator, I want to configure the loyalty cycle start and end dates, so that the reward period aligns with business planning periods.

#### Acceptance Criteria

1. THE Configuration_Service SHALL allow administrators to set the Loyalty_Cycle start date and end date (default: June 1 to next year May 31) with a minimum cycle duration of 30 days and a maximum cycle duration of 730 days, with dates interpreted in System_Time_Zone
2. WHEN 23:59:59 in System_Time_Zone on the Loyalty_Cycle end date is reached, THE Loyalty_System SHALL reset all customer purchase counts to zero for the new cycle
3. WHEN the Loyalty_Cycle resets, THE Loyalty_System SHALL archive the previous cycle data for historical reference
4. IF an administrator attempts to save a Loyalty_Cycle definition that has an end date on or before the start date, THEN THE Configuration_Service SHALL reject the configuration and display an error message indicating the invalid dates
5. WHEN an administrator modifies the Loyalty_Cycle dates, THE Configuration_Service SHALL apply the change to the next cycle without affecting the current active cycle
6. THE Loyalty_System SHALL display the current Loyalty_Cycle status including start date, end date, and days remaining on the admin dashboard

### Requirement 4: Configurable Purchase Thresholds and Gift Tiers

**User Story:** As an administrator, I want to configure the purchase count thresholds that trigger gift eligibility and define gift tiers, so that reward criteria can be adjusted based on business strategy.

#### Acceptance Criteria

1. THE Configuration_Service SHALL allow administrators to define between 1 and 10 Purchase_Threshold values, each specified as a positive integer representing a purchase count between 1 and 100 (default thresholds: 3rd and 6th purchases)
2. WHEN a customer's qualifying purchase count within the current Loyalty_Cycle reaches a configured and enabled Purchase_Threshold, THE Loyalty_System SHALL mark that customer as an Eligible_Customer for the corresponding gift tier
3. THE Configuration_Service SHALL allow administrators to associate a Gift_Type (Cash_Return or Gift_Item), a gift description (maximum 200 characters), and a gift value (between 0.01 and 999,999.99 BDT) with each Purchase_Threshold
4. WHEN a Purchase_Threshold configuration is modified, THE Loyalty_System SHALL apply the new threshold to future purchases without retroactively changing existing eligibility
5. THE Configuration_Service SHALL allow administrators to enable or disable individual Purchase_Threshold tiers without deleting the configuration
6. IF a customer's purchase count reaches a Purchase_Threshold that is currently disabled, THEN THE Loyalty_System SHALL not mark the customer as eligible and SHALL not trigger any notification for that tier
7. THE Configuration_Service SHALL allow administrators to set a minimum purchase amount per transaction (between 0.01 and 999,999.99 BDT) that qualifies toward the Purchase_Threshold count
8. THE Configuration_Service SHALL allow administrators to define excluded product categories that do not count toward Purchase_Threshold progression
9. IF an administrator attempts to save a Purchase_Threshold value that duplicates an existing threshold within the same Loyalty_Cycle, THEN THE Configuration_Service SHALL reject the configuration and display an error message indicating the duplicate value

### Requirement 5: SMS Verification and Gift Redemption

**User Story:** As a store manager, I want to verify customer gift eligibility via SMS at my outlet, so that gifts are redeemed only at the designated outlet with proper verification.

#### Acceptance Criteria

1. WHEN a customer becomes an Eligible_Customer, THE Notification_Service SHALL send an SMS to the customer containing a unique 6-digit numeric verification code, the gift description, and the designated outlet name within 5 minutes of eligibility being established
2. THE Gift_Redemption_Service SHALL bind each verification code to a single specific outlet based on the customer's most recent qualifying purchase location
3. WHEN a store manager enters a verification code that matches an issued code, is not expired, and has not been previously redeemed, THE Gift_Redemption_Service SHALL mark the gift as redeemed, record the redemption timestamp, outlet, staff member identifier, and the Gift_Type dispensed
4. IF a customer attempts to redeem a verification code at an outlet other than the designated outlet, THEN THE Gift_Redemption_Service SHALL reject the redemption and display a message indicating the correct designated outlet name
5. IF a verification code has already been redeemed, THEN THE Gift_Redemption_Service SHALL reject the redemption attempt and display a message indicating the gift was already claimed with the redemption date
6. THE Gift_Redemption_Service SHALL allow each verification code to be used exactly once
7. THE Gift_Redemption_Service SHALL expire unredeemed verification codes after a configurable number of days (default: 30 days, configurable range: 7 to 90 days) from issuance
8. IF an expired verification code is presented, THEN THE Gift_Redemption_Service SHALL reject the redemption and inform the store manager that the code has expired
9. THE Gift_Redemption_Service SHALL provide a search interface for store managers to look up customer eligibility by phone number or verification code, displaying customer name, gift tier, verification code status (active, redeemed, or expired), and designated outlet
10. IF a verification code is entered that does not match any issued code or does not conform to the 6-digit numeric format, THEN THE Gift_Redemption_Service SHALL reject the attempt and display a message indicating the code is invalid
11. IF more than 5 failed redemption attempts are made for the same verification code within a 15-minute window, THEN THE Gift_Redemption_Service SHALL temporarily block further attempts for that code for 30 minutes

### Requirement 6: Customer Profile and Purchase Tracking

**User Story:** As a store manager, I want to view a customer's loyalty profile, so that I can verify their loyalty status and assist with redemption.

#### Acceptance Criteria

1. THE Loyalty_System SHALL maintain a Customer_Profile for each customer containing name, phone number, total qualifying purchases in the current Loyalty_Cycle, current progress toward the next Purchase_Threshold, and redemption history for the current Loyalty_Cycle
2. WHEN a store manager searches for a customer by phone number, THE Loyalty_System SHALL display the Customer_Profile within 3 seconds
3. IF a store manager searches for a phone number that does not match any customer record, THEN THE Loyalty_System SHALL display a message indicating no customer was found
4. THE Loyalty_System SHALL display the customer's progress toward the next Purchase_Threshold as a numerical count (e.g., "2 of 3 purchases")
5. IF a customer has reached all configured Purchase_Thresholds in the current Loyalty_Cycle, THEN THE Loyalty_System SHALL display a completion status indicating all reward tiers have been achieved
6. THE Loyalty_System SHALL display all verification codes associated with a customer within the current Loyalty_Cycle, showing the code status (active, redeemed, or expired), associated gift tier, designated outlet, and issuance date

### Requirement 7: Role-Based Access Control

**User Story:** As a system administrator, I want to manage user access through roles, so that each user can only access features appropriate to their responsibility.

#### Acceptance Criteria

1. THE RBAC_Service SHALL support the following roles stored as role assignments in DynamoDB user records: Admin and Outlet_Manager
2. THE RBAC_Service SHALL enforce role-based authorization where Admin users have full access to all system features and Outlet_Manager users have access only to redemption, customer lookup, and their assigned outlet data
3. WHEN a user attempts to access a resource without the required role permission, THE RBAC_Service SHALL deny access and return an HTTP 403 response
4. THE RBAC_Service SHALL allow Admin users to create and manage user accounts, set passwords, and assign roles through the admin interface, with user records persisted in DynamoDB
5. THE RBAC_Service SHALL support outlet-scoped permissions where an Outlet_Manager can only access data and perform redemptions for their assigned outlet
6. WHEN a user submits valid credentials via the Auth_Login_Page, THE Auth_Lambda SHALL authenticate the user against credentials stored in DynamoDB and issue a signed JWT token containing the user identifier, role, and assigned outlet identifier (if applicable) with a configurable expiration (default: 8 hours)
7. IF an API request is received without a valid JWT token or with an expired JWT token, THEN THE Loyalty_System SHALL reject the request with HTTP status code 401
8. WHEN a user authenticates successfully, THE Loyalty_System SHALL extract the user's role and permissions from the JWT token claims issued by the Auth_Lambda
9. THE Auth_Lambda SHALL store user passwords using bcrypt hashing with a minimum cost factor of 12

### Requirement 8: Outlet Management

**User Story:** As an administrator, I want to manage outlet information, so that the loyalty program correctly maps transactions and redemptions to physical store locations.

#### Acceptance Criteria

1. THE Loyalty_System SHALL maintain an outlet registry containing outlet identifier, name, address, phone number, and assigned Outlet_Manager
2. THE Configuration_Service SHALL allow administrators to add, update, and deactivate outlets
3. WHEN an outlet is deactivated, THE Loyalty_System SHALL prevent new redemptions at that outlet
4. IF an outlet is the only active outlet remaining, THEN THE Configuration_Service SHALL prevent deactivation and display a message indicating that at least one outlet must remain active
5. THE Loyalty_System SHALL display outlet status (active or inactive) on the outlet management screen

### Requirement 9: Audit Trail

**User Story:** As an administrator, I want a record of key system actions, so that I can investigate issues and maintain accountability.

#### Acceptance Criteria

1. THE Audit_Service SHALL record all gift redemption events including customer identifier, outlet, staff member, verification code, and redemption timestamp
2. THE Audit_Service SHALL record all configuration changes including the parameter changed, old value, new value, and the administrator who made the change
3. THE Audit_Service SHALL record all data ingestion job results including job type (API or Excel), status, records processed, and timestamp
4. THE Audit_Service SHALL retain audit records for a minimum of 3 years
5. THE Audit_Service SHALL store audit records in an append-only manner that prevents modification or deletion by any user role

### Requirement 10: AWS Serverless Deployment

**User Story:** As a technical lead, I want the system deployed on AWS serverless infrastructure, so that operational costs remain low and the system scales automatically.

#### Acceptance Criteria

1. THE Loyalty_System SHALL use DynamoDB as the primary data store with on-demand capacity mode and single-table design
2. THE Loyalty_System SHALL deploy the API layer using .NET 8 Minimal API on AWS Lambda with Native AOT compilation, configured with a maximum memory allocation of 512MB and a timeout of 30 seconds per invocation
3. THE Loyalty_System SHALL host the React frontend on AWS S3 with CloudFront distribution
4. THE Loyalty_System SHALL use a custom authentication service implemented as an Auth_Lambda endpoint that authenticates users against DynamoDB-stored credentials and issues signed JWT tokens for role management
5. THE Loyalty_System SHALL integrate with a third-party SMS gateway for sending verification codes and notifications
6. THE Loyalty_System SHALL expose a RESTful API supporting JSON request and response formats with URL path versioning (e.g., /api/v1/)
7. THE Loyalty_System SHALL deploy all infrastructure in the ap-south-1 (Mumbai) AWS region

### Requirement 11: React Frontend (MVP)

**User Story:** As an end user, I want a web interface to interact with the loyalty system, so that I can perform my role-specific tasks efficiently.

#### Acceptance Criteria

1. THE Loyalty_System SHALL provide a React-based single-page application as the user interface, hosted on S3 with CloudFront
2. THE Loyalty_System SHALL implement responsive design that renders correctly on desktop (viewport width 1024px and above) and tablet (viewport width 768px to 1023px) screen sizes
3. THE Loyalty_System SHALL organize features into menu sections based on the user's role: Admin users see Dashboard, Customers, Redemptions, Configuration, Outlets, and Users; Outlet_Manager users see Redemptions and Customers
4. WHEN a user authenticates via the Auth_Login_Page, THE Loyalty_System SHALL redirect to the appropriate landing page based on role (Dashboard for Admin, Redemptions for Outlet_Manager)
5. THE Loyalty_System SHALL display a loading indicator within 300 milliseconds of any user-initiated action and display a success or error message upon completion
6. THE Loyalty_System SHALL implement client-side form validation with inline error messages before submitting requests to the API
7. IF a user navigates to a section for which they lack permission, THEN THE Loyalty_System SHALL display a message indicating insufficient permissions and redirect to their landing page

### Requirement 12: Notification Delivery

**User Story:** As a customer, I want to receive SMS notifications about my gift eligibility, so that I know when and where to claim my reward.

#### Acceptance Criteria

1. WHEN a customer reaches a Purchase_Threshold, THE Notification_Service SHALL send an SMS containing the customer name, gift description, designated outlet name, and the verification code
2. WHEN a verification code is within 7 days of expiration, THE Notification_Service SHALL send a reminder SMS to the customer containing the verification code, designated outlet name, and the expiration date
3. THE Notification_Service SHALL maintain a log of all sent notifications with delivery status (sent, delivered, or failed), recipient phone number, and timestamp
4. IF an SMS delivery fails, THEN THE Notification_Service SHALL retry delivery up to 3 times with 1-hour intervals between attempts
5. IF all retry attempts are exhausted without successful delivery, THEN THE Notification_Service SHALL mark the notification as permanently failed and log the failure reason
6. IF a customer's phone number is missing or invalid (not conforming to Valid_Phone_Number format), THEN THE Notification_Service SHALL log the notification as undeliverable with the reason
