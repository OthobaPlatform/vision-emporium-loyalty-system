# Requirements Document

## Introduction

The Vision Emporium Loyalty System is a configurable customer loyalty and rewards platform for Vision Emporium, an electronic goods retail chain under the Pran RFL Group. The system tracks customer purchases across outlets, identifies repeat buyers who reach configurable purchase thresholds (e.g., 3rd and 6th purchases), awards gifts, and manages outlet-specific SMS-verified gift redemption. It includes analytics dashboards, role-based access control, notification services, audit logging, and integrates with external data sources via API and Excel file imports. The system operates on a configurable Loyalty_Cycle (default: one year, configurable between 30 and 730 days) that resets loyalty progress at the end of each cycle.

## Glossary

- **Loyalty_System**: The core application managing customer loyalty tracking, gift eligibility, and redemption workflows
- **Data_Ingestion_Service**: The component responsible for importing sales and customer data from external APIs and Excel files
- **Loyalty_Cycle**: A configurable time period (default: June 1 to next year May 31) during which customer purchases are tracked for loyalty rewards
- **Purchase_Threshold**: A configurable number of purchases (e.g., 3rd, 6th) that triggers gift eligibility for a customer
- **Gift_Redemption_Service**: The component that handles outlet-specific verification and one-time gift claim processing
- **SMS_Verification**: A message sent to eligible customers containing a unique verification code tied to a specific outlet
- **Outlet**: A physical Vision Emporium retail store location
- **Dashboard_Service**: The analytics component providing visualizations of customer trends and loyalty metrics
- **RBAC_Service**: The role-based access control and policy-based authorization component
- **Configuration_Service**: The component managing all configurable parameters including cycles, thresholds, and rules
- **Customer**: A person who makes purchases at Vision Emporium outlets
- **Eligible_Customer**: A customer who has reached a configured Purchase_Threshold within the current Loyalty_Cycle
- **Notification_Service**: The component responsible for sending SMS, email, and in-app notifications to customers and staff
- **Audit_Service**: The component that records all system actions for compliance and traceability
- **Gift_Inventory_Service**: The component managing available gift stock per outlet and gift type
- **Report_Service**: The component generating exportable reports for management review
- **Customer_Profile**: The aggregated view of a customer's purchase history, loyalty status, and redemption records
- **Outlet_Manager**: A user role responsible for managing a specific outlet's redemption operations
- **Sync_Job**: A scheduled or on-demand data synchronization task between external systems and the Loyalty_System
- **Gift_Type**: A classification of a gift associated with a Purchase_Threshold tier indicating how the gift is dispensed; permitted values are Cash_Return and Gift_Item
- **Cash_Return**: A Gift_Type dispensed as a monetary refund or store credit with a configured cash amount, where redemption does not decrement Gift_Inventory_Service stock
- **Gift_Item**: A Gift_Type dispensed as a physical product drawn from the Gift_Inventory_Service, where redemption decrements stock at the redeeming outlet
- **Transaction_Type**: A classification assigned to each transaction record indicating its role in loyalty progression; permitted values are paid_purchase, gift_redemption, and exchange
- **Paid_Purchase**: A transaction in which a customer pays for one or more items, recorded with Transaction_Type paid_purchase, that increments the customer's Purchase_Threshold count when the purchase amount meets the configured minimum and the product category is not in the excluded categories
- **Gift_Redemption_Transaction**: A transaction recorded when a customer receives a Gift_Item or a Cash_Return payout as part of a loyalty redemption, recorded with Transaction_Type gift_redemption, that does not increment the customer's Purchase_Threshold count
- **Exchange_Transaction**: A transaction in which a customer swaps a previously purchased item for another item of equivalent value with no additional payment, recorded with Transaction_Type exchange and a reference to the original Paid_Purchase, that does not increment the customer's Purchase_Threshold count
- **Return_Request**: A customer-initiated request to return a previously purchased item for a refund, which is not permitted by the Loyalty_System
- **System_Currency**: The single monetary currency used by the Loyalty_System for all amounts (purchase amount, gift value, Cash_Return amount, store credit, reports). Fixed to Bangladeshi Taka (BDT, ISO 4217), with all amounts stored as decimals with two fractional digits
- **System_Time_Zone**: The single business time zone used for Loyalty_Cycle boundaries, scheduling, cycle-reset evaluation, and reporting; fixed to Asia/Dhaka (UTC+06:00). All persisted timestamps are stored in ISO 8601 UTC and presented in System_Time_Zone in the UI
- **Customer_Quiet_Hours_Zone**: The time zone used for customer-facing notification quiet hours; equal to the time zone of the customer's designated Outlet, falling back to System_Time_Zone when no designated outlet exists
- **Store_Credit_Balance**: A per-Customer monetary balance accumulated from Cash_Return payouts dispensed as store credit, denominated in System_Currency, that is automatically applied to reduce the payable amount on the customer's next qualifying Paid_Purchase up to the available balance
- **Verification_Code_State**: The lifecycle state of a verification code; permitted values are issued, active, redeemed, expired, reassigned, and blocked
- **PII**: Personally identifiable information held by the Loyalty_System, including customer name, phone number, address, store-credit balance, and purchase history
- **Valid_Phone_Number**: A phone number conforming to E.164 format with an explicit country code, defaulting to Bangladesh (+880) when no country code is supplied; the value is normalised to E.164 before persistence and validation
- **Gift_Catalog_Item**: A persisted catalogue entry describing a physical gift available for redemption (SKU, display name, description, image reference, monetary value in System_Currency, and active/archived status); referenced by Gift_Inventory_Service per-outlet stock entries and by Purchase_Threshold tiers with Gift_Type set to Gift_Item
- **Customer_Identity_Key**: The natural key used to resolve a Customer across data sources; defined as the Valid_Phone_Number
- **Business_Owner**: A named organisational accountability role responsible for product and UAT signoff on Loyalty_System releases; mapped to one or more named users via the Configuration_Service and not itself an RBAC_Service authorisation role
- **Technical_Lead**: A named organisational accountability role responsible for disaster-recovery drills, cost guardrails, and incident response; mapped to one or more named users via the Configuration_Service and not itself an RBAC_Service authorisation role
- **Finance_Team**: A named organisational accountability role responsible for Cash_Return payout escalations and financial reconciliation; mapped to one or more named users via the Configuration_Service and not itself an RBAC_Service authorisation role
- **Customer_Portal**: The customer-facing surface (web and SMS-initiated deep links) through which a Customer authenticates via a one-time SMS code tied to their Valid_Phone_Number to view their Customer_Profile, Store_Credit_Balance, active verification codes, and to submit data-erasure or data-export requests
- **AWS_Primary_Region**: The single AWS region designated as the primary deployment region for the Loyalty_System; fixed to `ap-south-1` (Mumbai) to satisfy Bangladesh data-residency expectations, with the secondary replication region defined in Requirement 21 AC4
- **SMS_Gateway_Provider**: A third-party SMS delivery vendor integrated by the Notification_Service; the Loyalty_System SHALL be configured with a primary provider and at least one secondary failover provider

## Requirements

### Requirement 1: Data Ingestion via External API

**User Story:** As an administrator, I want to ingest sales and customer data from an external API, so that the Loyalty_System has up-to-date purchase records without manual entry.

#### Acceptance Criteria

1. WHEN an external API endpoint is configured, THE Data_Ingestion_Service SHALL fetch sales transaction data including customer identifier, customer name, customer phone number, outlet identifier, purchase date, purchase amount, product category, and Transaction_Type (with permitted values paid_purchase, gift_redemption, and exchange)
2. WHEN the Data_Ingestion_Service receives an API response with HTTP status 200 and a parseable response body, THE Data_Ingestion_Service SHALL validate each transaction record for the presence of all required fields (customer identifier, customer phone number, outlet identifier, purchase date, purchase amount, and Transaction_Type) and store valid records in DynamoDB with a processing timestamp and the classified Transaction_Type
3. IF the external API returns an HTTP error status (4xx or 5xx) or is unreachable within 30 seconds, THEN THE Data_Ingestion_Service SHALL log the failure with timestamp and error details and retry up to a configurable number of attempts (default: 3) with exponential backoff starting at 5 seconds and capped at a maximum delay of 5 minutes
4. IF all retry attempts are exhausted without a successful response, THEN THE Data_Ingestion_Service SHALL mark the Sync_Job as failed, record the failure in the ingestion log, and send an alert notification to administrators via the Notification_Service
5. WHEN duplicate transaction records are detected based on a matching source transaction identifier (when supplied) or, in the absence of a source transaction identifier, on a matching combination of customer identifier, outlet identifier, purchase date, purchase amount, and item identifier, THE Data_Ingestion_Service SHALL skip duplicate records and log the occurrence with the duplicate transaction identifier
6. THE Configuration_Service SHALL allow administrators to schedule Sync_Job execution at configurable intervals with a minimum interval of 5 minutes (default: 60 minutes)
7. WHEN a Sync_Job completes, THE Data_Ingestion_Service SHALL record the sync status (success, partial, or failed), records fetched, records stored, records skipped, and records rejected in an ingestion log
8. THE Data_Ingestion_Service SHALL support configurable API authentication methods including API key, OAuth 2.0, and basic authentication
9. IF a transaction record from the API response is missing one or more required fields or contains invalid data (non-numeric purchase amount, purchase amount outside 0.01 to 999,999,999.99, or unparseable purchase date), THEN THE Data_Ingestion_Service SHALL reject that record, continue processing remaining records, and log the rejected record with the reason for rejection
10. IF a transaction record from the API response is missing the Transaction_Type field, THEN THE Data_Ingestion_Service SHALL classify the record as paid_purchase by default and log the classification action with the transaction identifier
11. IF a transaction record from the API response contains a Transaction_Type value other than paid_purchase, gift_redemption, or exchange, THEN THE Data_Ingestion_Service SHALL reject that record, continue processing remaining records, and log the rejected record with the reason for rejection
12. WHEN a transaction record is stored with Transaction_Type gift_redemption or exchange, THE Data_Ingestion_Service SHALL flag the record as non-qualifying for Purchase_Threshold progression so that the Loyalty_System does not increment the customer's purchase count for that record

### Requirement 2: Data Ingestion via Excel File Import

**User Story:** As an administrator, I want to import sales and customer data from Excel files, so that I can bulk-load historical or offline transaction data into the Loyalty_System.

#### Acceptance Criteria

1. WHEN an administrator uploads an Excel file, THE Data_Ingestion_Service SHALL validate the file format against the expected schema requiring the following columns: customer identifier (non-empty string), customer name (non-empty string, maximum 200 characters), customer phone number (valid phone number format with country code), outlet identifier (matching an existing outlet in the system), purchase date (ISO 8601 date format, not in the future), purchase amount (numeric value between 0.01 and 999,999,999.99), product category (non-empty string), and Transaction_Type (one of paid_purchase, gift_redemption, or exchange)
2. WHEN the Excel file passes validation, THE Data_Ingestion_Service SHALL accept the upload via an asynchronous job pattern (upload to S3, enqueue a processing job, return a job identifier to the administrator) and SHALL parse and store all valid transaction records in DynamoDB within 5 minutes for files up to 500,000 rows, independently of API Gateway and Lambda synchronous timeouts
3. IF the Excel file contains rows with missing or invalid data, THEN THE Data_Ingestion_Service SHALL reject those rows, continue processing valid rows, and return a downloadable summary report listing each rejected row with its row number, the failing field name, and the specific rejection reason
4. WHEN an Excel import completes, THE Data_Ingestion_Service SHALL display the total records processed, records imported, records rejected, and records skipped as duplicates on the import status screen
5. THE Data_Ingestion_Service SHALL support Excel files in .xlsx format up to 50MB in size and up to 500,000 rows
6. WHEN an Excel file exceeds the size or row limit, THE Data_Ingestion_Service SHALL reject the file before processing and inform the administrator of the applicable limits
7. THE Data_Ingestion_Service SHALL provide a downloadable Excel template with the required column headers and sample data
8. WHEN duplicate transaction records are detected during Excel import (matching source transaction identifier when present, otherwise matching the combination of customer identifier, outlet identifier, purchase date, purchase amount, and item identifier), THE Data_Ingestion_Service SHALL skip duplicate records and include the count of skipped duplicates in the import summary
9. IF a row in the Excel file contains a Transaction_Type value other than paid_purchase, gift_redemption, or exchange, THEN THE Data_Ingestion_Service SHALL reject that row, continue processing remaining rows, and include the row number and rejection reason in the summary report
10. IF a row in the Excel file is missing the Transaction_Type column value, THEN THE Data_Ingestion_Service SHALL classify the row as paid_purchase by default and include the classification action in the import summary
11. WHEN an Excel row is stored with Transaction_Type gift_redemption or exchange, THE Data_Ingestion_Service SHALL flag the record as non-qualifying for Purchase_Threshold progression so that the Loyalty_System does not increment the customer's purchase count for that record

### Requirement 3: Configurable Loyalty Cycle

**User Story:** As an administrator, I want to configure the loyalty cycle start and end dates, so that the reward period aligns with business planning periods.

#### Acceptance Criteria

1. THE Configuration_Service SHALL allow administrators to set the Loyalty_Cycle start date and end date (default: June 1 to next year May 31, spanning two calendar years) with a minimum cycle duration of 30 days and a maximum cycle duration of 730 days, with dates interpreted in System_Time_Zone
2. WHEN 23:59:59 in System_Time_Zone on the Loyalty_Cycle end date is reached, THE Loyalty_System SHALL reset all customer purchase counts to zero for the new cycle
3. WHEN the Loyalty_Cycle resets, THE Loyalty_System SHALL archive the previous cycle data for historical reporting and retain archived data for a configurable retention period between 1 and 10 years (default: 3 years)
4. IF an administrator attempts to save a Loyalty_Cycle definition that overlaps with an existing cycle or has an end date on or before the start date, THEN THE Configuration_Service SHALL reject the configuration and display an error message indicating the conflicting dates
5. WHEN an administrator modifies the Loyalty_Cycle dates, THE Configuration_Service SHALL apply the change to the next cycle without affecting the current active cycle
6. THE Loyalty_System SHALL display the current Loyalty_Cycle status including days remaining and cycle progress percentage (as a whole number from 0 to 100) on the Dashboard
7. WHEN the Loyalty_Cycle has 30 days remaining, THE Notification_Service SHALL send an in-app notification and email to all administrators about the upcoming cycle reset including the reset date
8. WHEN a transaction record is ingested after the Loyalty_Cycle reset (Requirement 3 AC2) but carries a purchase date that falls within the just-closed cycle, THE Loyalty_System SHALL attribute the record to the closed cycle for archival reporting (Requirement 3 AC3) and SHALL NOT increment any Purchase_Threshold count in the new cycle for that record; late-arriving records with purchase dates older than 7 calendar days before the reset SHALL additionally be flagged for administrator review

### Requirement 4: Configurable Purchase Thresholds and Gift Tiers

**User Story:** As an administrator, I want to configure the purchase count thresholds that trigger gift eligibility and define gift tiers, so that reward criteria can be adjusted based on business strategy.

#### Acceptance Criteria

1. THE Configuration_Service SHALL allow administrators to define between 1 and 10 Purchase_Threshold values, each specified as a positive integer representing a purchase count between 1 and 100 (default: 3rd and 6th purchases)
2. WHEN a customer's purchase count within the current Loyalty_Cycle reaches a configured and enabled Purchase_Threshold, THE Loyalty_System SHALL mark that customer as an Eligible_Customer for the corresponding gift tier
3. THE Configuration_Service SHALL allow administrators to associate a Gift_Type (Cash_Return or Gift_Item), a gift description (maximum 200 characters), and a gift value (between 0.01 and 999,999.99 in local currency) with each Purchase_Threshold
4. WHEN a Purchase_Threshold configuration is modified, THE Loyalty_System SHALL apply the new threshold to future purchases without retroactively changing existing eligibility
5. THE Configuration_Service SHALL allow administrators to enable or disable individual Purchase_Threshold tiers without deleting the configuration
6. IF a customer's purchase count reaches a Purchase_Threshold that is currently disabled, THEN THE Loyalty_System SHALL not mark the customer as eligible and SHALL not trigger any notification for that tier
7. THE Configuration_Service SHALL allow administrators to set a minimum purchase amount per transaction (between 0.01 and 999,999.99 in local currency) that qualifies toward the Purchase_Threshold count
8. THE Configuration_Service SHALL allow administrators to define excluded product categories that do not count toward Purchase_Threshold progression
9. IF an administrator attempts to save a Purchase_Threshold value that duplicates an existing threshold within the same Loyalty_Cycle, THEN THE Configuration_Service SHALL reject the configuration and display an error message indicating the duplicate value
10. WHERE a Purchase_Threshold is configured with Gift_Type set to Gift_Item, THE Configuration_Service SHALL require administrators to select a specific gift item from the Gift_Inventory_Service catalog before saving the configuration
11. WHERE a Purchase_Threshold is configured with Gift_Type set to Cash_Return, THE Configuration_Service SHALL require administrators to specify the Cash_Return amount as a positive decimal between 0.01 and 999,999.99 in local currency before saving the configuration
12. IF an administrator attempts to save a Purchase_Threshold configuration without specifying a Gift_Type, THEN THE Configuration_Service SHALL reject the configuration and display an error message indicating that Gift_Type is required
13. WHERE a Purchase_Threshold is configured with Gift_Type set to Cash_Return, THE Configuration_Service SHALL not require gift item selection or any Gift_Inventory_Service stock association for that tier

### Requirement 5: Outlet-Wise SMS Verification for Gift Redemption

**User Story:** As a store manager, I want to verify customer gift eligibility via SMS at my outlet, so that gifts are redeemed only at the designated outlet with proper verification.

#### Acceptance Criteria

1. WHEN a customer becomes an Eligible_Customer, THE Notification_Service SHALL send an SMS to the customer containing a verification code that is unique among all currently active verification codes, formatted as a 6-digit numeric string, the gift description, and the designated outlet name and address within 60 seconds of eligibility being established, except where Customer_Quiet_Hours_Zone quiet hours apply per Requirement 12
2. THE Gift_Redemption_Service SHALL bind each verification code to a single specific outlet based on the customer's most recent qualifying purchase location, and SHALL preserve that binding for the lifetime of the code unless the outlet is later deactivated and reassigned per Requirement 16
3. WHEN a customer presents a verification code at the designated outlet that matches an issued code, is not expired, and has not been previously redeemed, THE Gift_Redemption_Service SHALL require that the processing staff member is authenticated through an active RBAC_Service session at the redeeming outlet, mark the gift as redeemed, record the redemption timestamp, outlet, authenticated staff member identifier, and the Gift_Type dispensed (Cash_Return or Gift_Item), and create a Gift_Redemption_Transaction with Transaction_Type gift_redemption that does not increment the customer's Purchase_Threshold count
4. IF a customer attempts to redeem a verification code at an outlet other than the designated outlet, THEN THE Gift_Redemption_Service SHALL reject the redemption and display a message indicating the designated outlet name and address
5. IF a verification code has already been redeemed, THEN THE Gift_Redemption_Service SHALL reject the redemption attempt and display a message indicating the gift was already claimed with the redemption date
6. THE Gift_Redemption_Service SHALL allow each verification code to be used exactly once
7. THE Gift_Redemption_Service SHALL expire unredeemed verification codes after a configurable number of days (default: 30 days, configurable range: 7 to 90 days) from issuance
8. IF an expired verification code is presented, THEN THE Gift_Redemption_Service SHALL reject the redemption and inform the customer that the code has expired along with the original expiration date
9. THE Gift_Redemption_Service SHALL provide a search interface for store managers to look up customer eligibility by phone number or verification code and display results within 2 seconds including customer name, phone number, gift tier, verification code status (active, redeemed, or expired), designated outlet, and issuance date
10. IF a verification code is entered that does not match any issued code or does not conform to the 6-digit numeric format, THEN THE Gift_Redemption_Service SHALL reject the attempt and display a message indicating the code is invalid
11. IF more than 5 failed redemption attempts are made for the same verification code within a 15-minute window, THEN THE Gift_Redemption_Service SHALL temporarily block further redemption attempts for that code for 30 minutes and notify the assigned Outlet_Manager
12. WHERE the redeemed Purchase_Threshold tier has Gift_Type set to Gift_Item, THE Gift_Redemption_Service SHALL instruct the Gift_Inventory_Service to decrement the corresponding gift item stock by 1 at the redeeming outlet and SHALL record the dispensed gift item identifier on the redemption record
13. WHERE the redeemed Purchase_Threshold tier has Gift_Type set to Cash_Return, THE Gift_Redemption_Service SHALL initiate a Cash_Return payout for the configured Cash_Return amount using one of three supported payout methods (cash from till, store credit, or bank transfer), record the payout amount and selected payout method on the redemption record, and SHALL not invoke the Gift_Inventory_Service to decrement any stock
14. WHEN a Cash_Return payout is initiated, THE Gift_Redemption_Service SHALL require the processing staff member to select and confirm exactly one payout method from cash from till, store credit, or bank transfer, capture the confirmation along with the staff member identifier and confirmation timestamp in the redemption record, complete the payout processing within 60 seconds, and mark the redemption as complete only after successful payout confirmation is received
15. IF a Cash_Return payout cannot be completed at the time of redemption due to insufficient till cash, bank transfer failure, or store credit system unavailability, THEN THE Gift_Redemption_Service SHALL keep the verification code in active status, record the failure reason and failure timestamp on the redemption attempt, and allow up to 3 retry attempts within the verification code's validity period
16. IF 3 Cash_Return payout retry attempts have failed for the same verification code, THEN THE Gift_Redemption_Service SHALL escalate the case to the assigned Outlet_Manager and the Finance_Team within 15 minutes of the third failure, mark the redemption as pending manual resolution, and prevent further automated retry attempts until the escalation is resolved by an authorized user
17. WHERE the selected Cash_Return payout method is store credit, THE Gift_Redemption_Service SHALL credit the configured Cash_Return amount to the customer's store credit balance, record the updated balance on the redemption record, make the credit available for application against future purchases at any outlet within 60 seconds of payout completion, and apply the credit automatically to reduce the payable amount on the customer's next qualifying purchase up to the available balance
18. THE Store_Credit_Balance SHALL persist across Loyalty_Cycle resets and SHALL NOT be reset when purchase counts are reset per Requirement 3 AC2; the balance SHALL expire only after 24 months of customer inactivity (no Paid_Purchase, no Exchange_Transaction, no redemption), at which point the remaining balance SHALL be written off to zero and the write-off SHALL be recorded by the Audit_Service
19. WHEN an Admin or Super_Admin invokes the verification-code re-issuance workflow for a code in active or expired state due to a documented SMS-delivery or customer-loss reason, THE Gift_Redemption_Service SHALL transition the original code to state reassigned, generate a new code unique among currently active codes bound to the same designated outlet and gift tier, issue an SMS containing the new code, and record the re-issuance event (administrator identifier, reason, original code reference, new code reference, timestamp) in the Audit_Service; verification-code re-issuance SHALL be permitted at most twice per original eligibility event
20. WHEN a Paid_Purchase applies any portion of a customer's Store_Credit_Balance per AC17, THE Loyalty_System SHALL debit the balance using an atomic conditional write that succeeds only if the current balance is greater than or equal to the requested debit amount, so that two concurrent purchases cannot collectively spend more than the available balance; on debit-condition failure THE Loyalty_System SHALL retry the read-debit cycle up to 3 times before rejecting the credit application and processing the purchase at the full payable amount

### Requirement 6: Gift Inventory Management

**User Story:** As an administrator, I want to manage gift stock across outlets for Gift_Item tiers, so that eligible customers can receive their physical gifts without stock shortages.

#### Acceptance Criteria

1. THE Gift_Inventory_Service SHALL track available gift quantities only for Purchase_Threshold tiers configured with Gift_Type set to Gift_Item, maintaining stock values per gift item per outlet as non-negative integers with a maximum of 10,000 units per gift item per outlet
2. WHEN gift stock for a specific Gift_Item at an outlet falls below a configurable threshold (default: 5 units), THE Notification_Service SHALL alert administrators with the outlet name, gift item identifier, and remaining quantity
3. THE Gift_Inventory_Service SHALL allow administrators to add, transfer, and adjust gift stock between outlets for Gift_Item entries only, recording the administrator, timestamp, operation type, source outlet, destination outlet, gift item identifier, and quantity for each operation
4. WHEN a Gift_Item is redeemed, THE Gift_Inventory_Service SHALL decrement the stock count for the corresponding gift item at the redemption outlet by 1 using an atomic conditional write that succeeds only if the current stock count is greater than or equal to 1, so that two concurrent redemptions of the last unit cannot both succeed
5. WHEN a Cash_Return is redeemed, THE Gift_Inventory_Service SHALL not modify any physical gift stock and SHALL not be invoked by the Gift_Redemption_Service for stock decrement operations
6. IF a Gift_Item is out of stock at the designated outlet during redemption, THEN THE Gift_Redemption_Service SHALL notify the store manager and place the redemption in a pending state for a configurable duration (default: 14 days), after which the pending redemption SHALL be automatically cancelled and the customer notified
7. IF an administrator attempts a stock transfer where the requested quantity exceeds the available stock at the source outlet, THEN THE Gift_Inventory_Service SHALL reject the transfer and display a message indicating the available quantity at the source outlet
8. IF a stock adjustment or redemption would result in a negative stock value, THEN THE Gift_Inventory_Service SHALL reject the operation and display a message indicating that stock cannot be reduced below zero
9. WHERE a Purchase_Threshold tier has Gift_Type set to Cash_Return, THE Gift_Inventory_Service SHALL not display, allow configuration of, or alert on stock levels for that tier

### Requirement 7: Customer Profile and Purchase History

**User Story:** As a store manager, I want to view a customer's complete loyalty profile, so that I can provide informed service and verify their loyalty status.

#### Acceptance Criteria

1. THE Loyalty_System SHALL maintain a Customer_Profile for each customer containing name, phone number, total purchases in current Loyalty_Cycle, current loyalty tier, and redemption history for the current Loyalty_Cycle
2. WHEN a store manager searches for a customer by phone number, THE Loyalty_System SHALL display the Customer_Profile within 2 seconds
3. IF a store manager searches for a phone number that does not match any customer record, THEN THE Loyalty_System SHALL display a message indicating no customer was found for the entered phone number
4. WHEN a store manager views a Customer_Profile, THE Loyalty_System SHALL display the customer's purchase history with date, outlet, amount, and product category for each transaction, sorted by date descending, with paginated results showing 20 transactions per page
5. WHEN a store manager views a Customer_Profile, THE Loyalty_System SHALL display the customer's exchange history in a section separate from purchase history, showing for each Exchange_Transaction the exchange date, outlet, original purchase reference (date and item), exchanged item, and exchange staff member, sorted by date descending and paginated at 20 transactions per page
6. THE Loyalty_System SHALL display the customer's progress toward the next Purchase_Threshold as a numerical count (e.g., "2 of 3 purchases") and visual progress indicator
7. IF a customer has reached all configured Purchase_Thresholds in the current Loyalty_Cycle, THEN THE Loyalty_System SHALL display a completion status indicating all reward tiers have been achieved
8. THE Loyalty_System SHALL display all active and expired verification codes associated with a customer within the current Loyalty_Cycle, showing the code, associated gift tier, designated outlet, issuance date, expiration date, and redemption status (unredeemed, redeemed, or expired)

### Requirement 8: Analytics Dashboard

**User Story:** As a business analyst, I want to view customer buying trends and loyalty metrics on a dashboard, so that I can make data-driven decisions about the loyalty program.

#### Acceptance Criteria

1. THE Dashboard_Service SHALL display customer purchase frequency trends over configurable time periods (daily, weekly, monthly, quarterly)
2. THE Dashboard_Service SHALL display the count and percentage of returning customers within the current Loyalty_Cycle, where a returning customer is defined as a customer with 2 or more purchases in the current Loyalty_Cycle
3. THE Dashboard_Service SHALL display outlet-wise sales performance including total transactions, total revenue, and average transaction value
4. THE Dashboard_Service SHALL display outlet-wise redemption statistics including total gifts issued, redeemed, pending, and expired
5. THE Dashboard_Service SHALL display the number of customers at each Purchase_Threshold level with a breakdown by outlet
6. THE Dashboard_Service SHALL provide data filtering by date range, outlet, product category, and customer segment where customer segment options are: Purchase_Threshold tier, purchase frequency range (1-2, 3-5, 6-10, 10+), and redemption status (redeemed, eligible unredeemed, not yet eligible)
7. THE Dashboard_Service SHALL render analytics visualizations within 3 seconds of page load for datasets up to 100,000 records
8. THE Dashboard_Service SHALL display a counter of total active customers, total purchases today, and total redemptions today, where an active customer is defined as a customer with at least 1 purchase in the current Loyalty_Cycle, and the counter SHALL refresh automatically every 60 seconds
9. THE Dashboard_Service SHALL display customer acquisition trends showing new customers (first purchase in current Loyalty_Cycle) versus returning customers (2 or more purchases in current Loyalty_Cycle) over configurable time periods (daily, weekly, monthly, quarterly)
10. THE Dashboard_Service SHALL provide comparison views between current and previous Loyalty_Cycle performance including total customers, total transactions, total revenue, total gifts issued, and total gifts redeemed
11. IF the requested dataset exceeds 100,000 records, THEN THE Dashboard_Service SHALL display a notification indicating that results are limited to the most recent 100,000 records and provide an option to export the full dataset via the Report_Service

### Requirement 9: Report Generation and Export

**User Story:** As a business analyst, I want to generate and export reports, so that I can share loyalty program performance data with stakeholders who do not have system access.

#### Acceptance Criteria

1. THE Report_Service SHALL generate reports in PDF and Excel formats and provide a download link for each generated report that remains accessible for the duration of the retention period
2. THE Report_Service SHALL support the following report types: Customer Summary, Outlet Performance, Redemption Status, Cycle Comparison, and Gift Inventory, each accepting filter parameters including date range, outlet, and Loyalty_Cycle
3. WHEN a user requests a report, THE Report_Service SHALL generate the report within 30 seconds for datasets up to 100,000 records
4. IF a report request targets a dataset exceeding 100,000 records, THEN THE Report_Service SHALL process the report asynchronously and notify the user via email when the report is available for download
5. THE Report_Service SHALL allow users to schedule recurring report generation (daily, weekly, monthly) with email delivery to up to 20 configured recipients per schedule
6. IF report generation fails due to a system error, THEN THE Report_Service SHALL notify the requesting user with an error message indicating the failure reason and allow the user to retry the request
7. THE Report_Service SHALL retain generated reports for a configurable period (default: 90 days) and automatically delete reports that exceed the retention period

### Requirement 10: Role-Based Access Control and Policy-Based Authorization

**User Story:** As a system administrator, I want to manage user access through roles and policies, so that each user can only access features and data appropriate to their responsibility.

#### Acceptance Criteria

1. THE RBAC_Service SHALL support the following roles: Super_Admin, Admin, Outlet_Manager, Analyst, and Viewer
2. THE RBAC_Service SHALL enforce policy-based authorization where each role has a defined set of permitted action types (create, read, update, delete, export) on specific resource types (customers, transactions, redemptions, reports, configurations, users, outlets, gift inventory)
3. WHEN a user attempts to access a resource without the required role or policy permission, THE RBAC_Service SHALL deny access and return an unauthorized response indicating the required role or permission that is missing
4. THE RBAC_Service SHALL allow Super_Admin users to create, modify, and delete role assignments for other users
5. IF a Super_Admin attempts to remove or downgrade the last remaining Super_Admin account, THEN THE RBAC_Service SHALL reject the operation and return an error message indicating that at least one Super_Admin must exist
6. THE RBAC_Service SHALL allow policy definitions to be configured without code changes through a policy management interface
7. WHEN a user's role is modified, THE RBAC_Service SHALL apply the new permissions on the user's next request within the current session (no re-authentication required) until the session is invalidated per Requirement 19 AC5 due to idle timeout or explicit logout
8. THE RBAC_Service SHALL support outlet-scoped permissions where an Outlet_Manager can only access data and perform actions for their assigned outlet
9. WHEN a permission change or access denial occurs, THE RBAC_Service SHALL log the event to the Audit_Service including the user identifier, timestamp, action attempted, target resource, and outcome (granted or denied)
10. THE RBAC_Service SHALL require multi-factor authentication for Super_Admin and Admin roles at login and before performing user role modifications or configuration changes
11. IF a Super_Admin or Admin user fails multi-factor authentication, THEN THE RBAC_Service SHALL deny the operation and log the failed attempt to the Audit_Service
12. WHEN a user account in the Super_Admin or Admin role authenticates for the first time, THE RBAC_Service SHALL require enrolment of at least one MFA factor before granting access to any protected resource and SHALL restrict the session to the MFA enrolment workflow until enrolment is completed
13. THE Configuration_Service SHALL maintain mappings from the organisational accountability roles Business_Owner, Technical_Lead, and Finance_Team to one or more named user accounts, and SHALL require at least one named user assigned to each organisational role at all times; IF an administrator attempts to remove the last named user from any organisational role, THEN THE Configuration_Service SHALL reject the operation and display a message indicating that at least one named user must remain assigned

### Requirement 11: Audit Trail and Activity Logging

**User Story:** As a compliance officer, I want a complete audit trail of all system actions, so that I can investigate issues and demonstrate regulatory compliance.

#### Acceptance Criteria

1. THE Audit_Service SHALL record all data modification operations including the user, timestamp in ISO 8601 UTC format, action type, affected resource, and before/after values
2. THE Audit_Service SHALL record all gift redemption events including customer identifier, outlet, staff member, verification code, and redemption timestamp
3. THE Audit_Service SHALL record all configuration changes including the parameter changed, old value, new value, and the administrator who made the change
4. THE Audit_Service SHALL retain audit records for a configurable period (default: 5 years, minimum: 1 year, maximum: 10 years)
5. WHEN a user searches the audit log with filtering by date range, user, action type, or resource, THE Audit_Service SHALL return paginated results (default: 50 records per page, maximum: 200) within 5 seconds for queries spanning up to 12 months of data
6. THE Audit_Service SHALL store audit records in an append-only manner that prevents modification or deletion by any user role including Super_Admin
7. IF the Audit_Service fails to persist an audit record, THEN THE Audit_Service SHALL retry the write up to 3 times and, if all retries fail, queue the record for deferred writing and alert administrators via the Notification_Service
8. THE Audit_Service SHALL record all authentication events including login attempts, login failures, logout, and role or permission changes
9. THE Audit_Service SHALL provide an export function that generates audit records in CSV format for a specified date range, filtered by user, action type, or resource, with exports limited to 1,000,000 records per request

### Requirement 12: Notification Service

**User Story:** As a customer, I want to receive timely notifications about my loyalty status and gift eligibility, so that I can claim my rewards before they expire.

#### Acceptance Criteria

1. WHEN a customer reaches a Purchase_Threshold, THE Notification_Service SHALL send an SMS notification within 5 minutes of the qualifying purchase being recorded (of which the verification-code issuance per Requirement 5 AC1 must complete within 60 seconds of eligibility being established), containing the customer name, gift description, designated outlet name and address, and the verification code
2. WHEN a verification code is within 7 days of expiration, THE Notification_Service SHALL send a reminder SMS to the customer containing the customer name, verification code, designated outlet name and address, and the expiration date
3. THE Notification_Service SHALL maintain a log of all sent notifications with delivery status (sent, delivered, failed), recipient phone number, notification type, and timestamp, retained for a minimum of 1 year
4. IF an SMS delivery fails, THEN THE Notification_Service SHALL retry delivery up to 3 times with 1-hour intervals between each attempt
5. IF all 3 retry attempts for an SMS delivery are exhausted without successful delivery, THEN THE Notification_Service SHALL mark the notification as permanently failed, record the failure reason in the notification log, and alert administrators via the Dashboard_Service
6. THE Configuration_Service SHALL allow administrators to customize notification message templates
7. WHILE quiet hours are active (default: 22:00 to 08:00 in the Customer_Quiet_Hours_Zone), THE Notification_Service SHALL queue all customer notifications and deliver them within 5 minutes after quiet hours end, in the order they were generated
8. IF a customer's phone number is missing or invalid (not matching a valid phone number format), THEN THE Notification_Service SHALL log the notification as undeliverable with the reason and flag the Customer_Profile for administrator review
9. THE Notification_Service SHALL be configured with a primary SMS_Gateway_Provider and at least one secondary SMS_Gateway_Provider, and IF the primary provider returns a delivery failure response or fails to respond within 30 seconds for 5 consecutive send attempts within a 10-minute window, THEN THE Notification_Service SHALL automatically fail over to the secondary provider for subsequent sends until the primary recovers, recording each failover transition in the Audit_Service
10. WHEN an administrator customises a notification message template per AC6, THE Configuration_Service SHALL retain the previous template version, require the change to be submitted as a draft, and require approval by a second administrator (Admin or Super_Admin distinct from the author) before the new version becomes active; the activation event SHALL be recorded in the Audit_Service with author, approver, template identifier, language, and version number, and SHALL support rollback to any previous version by an Admin or Super_Admin
11. WHEN a notification template version is activated, THE Configuration_Service SHALL require that English and Bangla variants per Requirement 20 AC6 are both present and approved for the same template identifier before activation; IF either language variant is missing or unapproved, THEN THE Configuration_Service SHALL reject the activation and display a message indicating the missing language variant

### Requirement 13: Cost-Optimized AWS Deployment

**User Story:** As a technical lead, I want the system deployed on AWS with minimal cost, so that operational expenses remain low while maintaining acceptable performance.

#### Acceptance Criteria

1. THE Loyalty_System SHALL use DynamoDB as the primary data store with on-demand capacity mode to minimize idle costs
2. THE Loyalty_System SHALL deploy the API layer using AWS Lambda with API Gateway, configured with a maximum memory allocation of 512MB and a timeout of 30 seconds per invocation, to eliminate always-on server costs
3. THE Loyalty_System SHALL host the React frontend on AWS S3 with CloudFront distribution for cost-effective static asset delivery
4. THE Loyalty_System SHALL use AWS Cognito for authentication to avoid building custom auth infrastructure
5. WHILE average API request rate is below 10 requests per second over a 5-minute window, THE Loyalty_System SHALL maintain only the minimum provisioned concurrency of zero reserved Lambda instances, relying on on-demand invocations
6. THE Loyalty_System SHALL use AWS SES for email notifications and integrate with a third-party SMS gateway for SMS delivery
7. THE Loyalty_System SHALL use AWS CloudWatch for monitoring and alerting with configurable alarm thresholds including defaults for API error rate (greater than 5% over 5 minutes), API latency (p95 greater than 5 seconds), and Lambda throttling (any throttle event)
8. THE Loyalty_System SHALL implement DynamoDB single-table design to minimize table costs and achieve single-digit millisecond read latency for single-item lookups
9. THE Loyalty_System SHALL use AWS Lambda layers for shared dependencies to keep individual function deployment packages below 50MB and achieve cold-start initialization within 2 seconds (consistent with Requirement 14 AC2)

### Requirement 14: API Layer

**User Story:** As a developer, I want a well-structured API layer that serves the frontend and integrates with external systems, so that the system is maintainable and extensible.

#### Acceptance Criteria

1. THE Loyalty_System SHALL expose a RESTful API supporting JSON request and response formats
2. THE Loyalty_System SHALL implement the API using .NET 8 Minimal API running on AWS Lambda with Native AOT compilation, achieving cold-start response times of no more than 2 seconds
3. WHEN an API request fails validation, THE Loyalty_System SHALL return a structured error response containing field-level error details (field name and validation failure reason for each invalid field) with HTTP status code 400
4. IF an API request is received without a valid JWT token issued by AWS Cognito, THEN THE Loyalty_System SHALL reject the request with HTTP status code 401 and an error response indicating the authentication failure reason
5. THE Loyalty_System SHALL version the API using URL path prefixes (e.g., /api/v1/)
6. IF an authenticated user exceeds the configured rate limit (default: 100 requests per minute), THEN THE Loyalty_System SHALL reject subsequent requests with HTTP status code 429 and include a response header indicating the number of seconds until the limit resets
7. THE Loyalty_System SHALL return paginated responses for all list endpoints with configurable page size (default: 20, maximum: 100), including total record count, current page number, total pages, and links to next and previous pages in the response
8. THE Loyalty_System SHALL include request correlation identifiers in all API responses for traceability

### Requirement 15: React Frontend

**User Story:** As an end user, I want a responsive and intuitive web interface, so that I can interact with the loyalty system efficiently from any device.

#### Acceptance Criteria

1. THE Loyalty_System SHALL provide a React-based single-page application as the user interface
2. THE Loyalty_System SHALL implement responsive design that renders all interactive elements accessible and all content readable without horizontal scrolling across desktop (viewport width 1024px and above), tablet (viewport width 768px to 1023px), and mobile (viewport width below 768px) screen sizes
3. WHEN a user navigates between sections, THE Loyalty_System SHALL render the target view within 2 seconds on a 10 Mbps network connection
4. THE Loyalty_System SHALL organize features into distinct menu sections: Dashboard, Customers, Redemptions, Reports, Configuration, and User Management, and SHALL display only the menu sections that the current user's role has permission to access as defined by the RBAC_Service
5. WHEN a user initiates any action (button click, form submission, navigation), THE Loyalty_System SHALL display a loading indicator within 200 milliseconds, and SHALL display a success confirmation or error message within 1 second of receiving the API response
6. THE Loyalty_System SHALL support dark mode and light mode, and SHALL persist the user's selected theme preference across browser sessions until explicitly changed by the user
7. THE Loyalty_System SHALL implement client-side form validation with inline error messages displayed adjacent to the invalid field before submitting requests to the API
8. THE Loyalty_System SHALL display the current user's role in the navigation header, and IF the user has an assigned outlet, THEN THE Loyalty_System SHALL also display the assigned outlet name
9. IF a user navigates to a section for which they lack permission, THEN THE Loyalty_System SHALL display a message indicating insufficient permissions and provide a link to return to the Dashboard

### Requirement 16: Outlet Management

**User Story:** As an administrator, I want to manage outlet information in the system, so that the loyalty program correctly maps transactions and redemptions to physical store locations.

#### Acceptance Criteria

1. THE Loyalty_System SHALL maintain an outlet registry containing outlet identifier, name, address, phone number, operating hours, and assigned Outlet_Manager, where outlet identifier, name, address, and phone number are required fields and operating hours and assigned Outlet_Manager are optional at creation
2. THE Configuration_Service SHALL allow administrators to add, update, activate, and deactivate outlets, and SHALL require outlet identifier, name, and address to be non-empty when adding or updating an outlet
3. WHEN an outlet is deactivated, THE Loyalty_System SHALL prevent new redemptions at that outlet and reassign pending verification codes to the geographically closest active outlet based on stored address coordinates
4. IF an outlet is the only active outlet remaining, THEN THE Configuration_Service SHALL prevent deactivation and display a message indicating that at least one outlet must remain active
5. WHEN pending verification codes are reassigned due to outlet deactivation, THE Notification_Service SHALL send an SMS to each affected customer indicating the new designated outlet name and address for their redemption
6. THE Loyalty_System SHALL display outlet status (active/inactive) and current gift inventory on the outlet management screen
7. THE Configuration_Service SHALL allow administrators to assign one or more Outlet_Manager users to each outlet, up to a maximum of 10 Outlet_Managers per outlet

### Requirement 17: Returns and Exchanges Policy

**User Story:** As a store manager, I want a clear returns and exchanges policy enforced by the system, so that customers can exchange items but cannot return items for refunds, and so loyalty progression remains accurate.

#### Acceptance Criteria

1. IF a customer submits a Return_Request through any channel (in-store interface, customer portal, or API), THEN THE Loyalty_System SHALL reject the Return_Request within 2 seconds and display a message indicating that returns are not permitted under the current store policy
2. WHEN a Return_Request is rejected, THE Audit_Service SHALL record the rejection event including customer identifier, outlet, staff member (if applicable), original purchase reference, timestamp, and rejection reason
3. THE Loyalty_System SHALL allow store managers to record an Exchange_Transaction in which a customer swaps a previously purchased item for a replacement item of equal or higher monetary value at the same outlet where the original purchase was made
4. WHEN a store manager initiates an Exchange_Transaction, THE Loyalty_System SHALL require selection of the original purchase reference (matching customer identifier, outlet, purchase date, and item) before the exchange can be saved
5. WHEN an Exchange_Transaction is recorded, THE Loyalty_System SHALL flag the transaction with Transaction_Type exchange and SHALL NOT increment the customer's purchase count toward any Purchase_Threshold
6. WHEN an Exchange_Transaction is recorded, THE Loyalty_System SHALL preserve the original Paid_Purchase's contribution to the customer's purchase count toward Purchase_Threshold progression
7. WHEN a transaction with Transaction_Type gift_redemption is ingested or recorded, THE Loyalty_System SHALL NOT increment the customer's purchase count toward any Purchase_Threshold
8. WHEN an Exchange_Transaction is recorded, THE Audit_Service SHALL record the event including customer identifier, outlet, staff member, original purchase reference, exchanged item identifier, replacement item identifier, value difference, and timestamp
9. THE Loyalty_System SHALL provide store managers with a user interface to process Exchange_Transactions that displays the customer's purchase history from the last 90 days filtered by the entered customer identifier or phone number, allows selection of the original purchase reference, and confirms the replacement item identifier and value before saving, with each user-initiated action returning a response within 3 seconds under normal load
10. IF a store manager attempts to record an Exchange_Transaction without specifying a valid original purchase reference for the same customer, THEN THE Loyalty_System SHALL reject the operation and display a message indicating that a valid original purchase reference is required
11. IF a store manager attempts to record an Exchange_Transaction where the original purchase date is more than 30 calendar days before the exchange date or the original purchase belongs to a Loyalty_Cycle that has already been archived, THEN THE Loyalty_System SHALL reject the operation and display a message indicating that the original purchase is outside the active exchange window
12. IF a store manager attempts to record an Exchange_Transaction at an outlet different from the outlet of the original purchase, THEN THE Loyalty_System SHALL reject the operation and display a message indicating that exchanges must be processed at the original purchase outlet
13. IF a store manager attempts to record an Exchange_Transaction where the replacement item's monetary value is less than the original item's monetary value, THEN THE Loyalty_System SHALL reject the operation and display a message indicating that the replacement item must be of equal or higher value and that no cash or credit refund will be issued for value differences
14. WHEN an Exchange_Transaction is recorded with a replacement item of higher monetary value than the original item, THE Loyalty_System SHALL require the customer to pay the value difference, SHALL record the differential amount paid against the Exchange_Transaction, and SHALL NOT count the differential amount as a new purchase toward any Purchase_Threshold
15. WHEN a customer purchases an additional item during the same visit as an Exchange_Transaction, THE Loyalty_System SHALL record the additional purchase as a separate transaction with Transaction_Type paid_purchase and SHALL increment the customer's Purchase_Threshold count by 1 for that additional purchase only when the purchase amount meets the configured minimum and the product category is not in the configured excluded categories
16. THE Loyalty_System SHALL NOT provide any user interface, API endpoint, or workflow that decrements a customer's purchase count or reverses Purchase_Threshold eligibility based on a returned, refunded, or cancelled purchase
17. IF a store manager attempts to record an Exchange_Transaction whose original purchase reference is itself a replacement item from a prior Exchange_Transaction, THEN THE Loyalty_System SHALL reject the operation and display a message indicating that a replacement item from a prior exchange cannot itself be exchanged

### Requirement 18: Data Privacy and Consent

**User Story:** As a customer, I want my personal information handled in compliance with applicable privacy regulations, so that I can trust the loyalty program with my data.

#### Acceptance Criteria

1. WHEN a Customer_Profile is created or updated during ingestion or in-store enrolment, THE Loyalty_System SHALL record an explicit consent flag indicating whether the customer has consented to loyalty-program participation and SMS marketing, the consent source (in-store, API, Excel import), and the consent timestamp
2. IF a customer's consent flag is not set or has been withdrawn, THEN THE Notification_Service SHALL NOT send marketing or eligibility SMS to that customer and SHALL log the suppressed notification with the reason
3. THE Loyalty_System SHALL provide an authenticated API endpoint and an administrator UI to record a data-erasure request, and WHEN a data-erasure request is approved by an Admin or Super_Admin, THE Loyalty_System SHALL anonymise the Customer_Profile (replace name, phone number, and address with non-identifying placeholders) within 30 calendar days while preserving transaction, redemption, and audit records for legal retention; Audit_Service entries SHALL NOT be modified or deleted by the anonymisation workflow per Requirement 11 AC6
4. THE Loyalty_System SHALL encrypt all PII at rest using AWS-managed keys (DynamoDB encryption at rest, S3 server-side encryption) and in transit using TLS 1.2 or higher
5. THE Loyalty_System SHALL restrict access to the customer phone number and address fields to roles that have an explicit pii:read policy permission, and SHALL mask all but the last four digits of the phone number for roles without that permission; the Outlet_Manager role SHALL be granted pii:read scoped to customers whose most recent qualifying purchase or designated outlet matches the Outlet_Manager's assigned outlet, and SHALL see masked values for all other customers
6. WHEN PII is accessed, exported, or modified, THE Audit_Service SHALL record the access including the user identifier, action type, target customer identifier, and timestamp
7. THE Configuration_Service SHALL allow administrators to configure the PII retention period (minimum 1 year, maximum 10 years, default 5 years), and WHEN a Customer_Profile has had no Paid_Purchase activity for the retention period, THE Loyalty_System SHALL automatically anonymise that Customer_Profile and log the action
8. IF a customer requests an export of their personal data through an authenticated channel, THEN THE Loyalty_System SHALL produce a machine-readable export (JSON) of the Customer_Profile, purchase history, redemption history, and consent records within 14 calendar days and notify the customer when the export is ready
9. THE Loyalty_System SHALL provide a Customer_Portal that authenticates a Customer via a one-time 6-digit numeric code sent over SMS to their registered Valid_Phone_Number, where the code expires after 10 minutes, is invalidated after one successful use, and is subject to the per-source-IP rate limit defined in Requirement 19 AC7; the Customer_Portal SHALL be the authenticated channel referenced in AC3 and AC8 for customer-initiated data-erasure and data-export requests, and SHALL allow the authenticated customer to view their Store_Credit_Balance, active verification codes, and language preference
10. WHEN an authenticated customer or an Admin or Super_Admin initiates a change to the Customer_Identity_Key (Valid_Phone_Number) for an existing Customer_Profile, THE Loyalty_System SHALL require successful one-time code verification on both the existing and the new phone numbers, reject the change if the new phone number already maps to another active Customer_Profile (in which case the merge workflow in Requirement 23 AC5 SHALL be used instead), update the Customer_Identity_Key on the profile, retain the prior phone number as a historical alias for forensic traceability without making it the natural key, and record the change in the Audit_Service

### Requirement 19: Security Controls

**User Story:** As a security officer, I want platform-wide security controls enforced by the system, so that customer data, financial payouts, and administrative actions are protected against common threats.

#### Acceptance Criteria

1. THE Loyalty_System SHALL enforce TLS 1.2 or higher for all HTTP traffic to API Gateway and CloudFront, and SHALL reject lower TLS versions with an HTTP-level connection failure
2. THE Loyalty_System SHALL store all secrets (third-party SMS gateway credentials, external API credentials, signing keys) in AWS Secrets Manager or AWS Systems Manager Parameter Store with encryption at rest, and SHALL NOT embed secrets in deployment artefacts, environment variables in source control, or log output
3. THE Loyalty_System SHALL validate and sanitise all API request inputs against the documented schema and SHALL reject requests that fail validation with HTTP 400 before any downstream processing
4. THE Loyalty_System SHALL implement protections against the OWASP API Security Top 10, including broken object level authorization (every resource access is checked against the requester's RBAC scope and outlet assignment), injection (parameterised DynamoDB expressions only), and unrestricted resource consumption (per-endpoint payload size limits and rate limits per Requirement 14 AC6)
5. WHEN an authenticated user session has been idle for 30 minutes (configurable between 5 and 120 minutes), THE Loyalty_System SHALL invalidate the session and require re-authentication on the next request
6. THE RBAC_Service SHALL provide backup recovery codes for users with MFA enabled and SHALL allow exactly one MFA factor reset per user within any 24-hour period, with the reset event recorded in the Audit_Service
7. THE Gift_Redemption_Service SHALL apply a per-source-IP rate limit on verification-code submissions of no more than 30 attempts per minute per IP across all codes, and IF this limit is exceeded, THEN THE Gift_Redemption_Service SHALL reject further attempts from that IP with HTTP 429 for the next 15 minutes; this limit is applied in addition to the per-authenticated-user rate limit defined in Requirement 14 AC6, and the more restrictive of the two SHALL take precedence
8. THE Loyalty_System SHALL emit structured logs (JSON) for all API requests including correlation identifier, user identifier, route, status code, and latency, and SHALL forward logs to CloudWatch Logs with a retention period of at least 90 days
9. THE Loyalty_System SHALL undergo automated dependency vulnerability scanning on every build, and IF a known vulnerability of severity High or Critical is detected in a deployed dependency, THEN THE build pipeline SHALL fail until the vulnerability is remediated or formally accepted by an authorised security reviewer

### Requirement 20: Accessibility, Localization, and Browser Support

**User Story:** As an end user with diverse abilities and language preferences, I want the application to be usable with assistive technologies and available in my preferred language, so that I can perform my tasks without barriers.

#### Acceptance Criteria

1. THE React frontend SHALL conform to WCAG 2.2 Level AA, including a minimum contrast ratio of 4.5:1 for normal text and 3:1 for large text and non-text UI components
2. THE React frontend SHALL be fully operable using a keyboard alone (Tab, Shift+Tab, Enter, Space, Arrow keys, Escape) and SHALL display a visible focus indicator on every focusable element with a contrast ratio of at least 3:1 against its background
3. THE React frontend SHALL provide a programmatic accessible name and role for every interactive element using native HTML semantics or WAI-ARIA, and SHALL associate every form input with a visible label
4. WHEN dynamic content changes (toast notifications, validation errors, redemption status changes), THE React frontend SHALL announce the change to assistive technologies via an ARIA live region without moving keyboard focus
5. THE React frontend SHALL support English and Bangla as user-selectable interface languages, persist the selected language across browser sessions for authenticated users, and use English as the default language for unauthenticated users
6. THE Notification_Service SHALL support English and Bangla message templates per notification type, and SHALL select the template matching the customer's recorded language preference, falling back to English when no preference is recorded
7. THE React frontend SHALL support the latest two stable major versions of Chrome, Edge, Firefox, and Safari at release time, and SHALL display a non-blocking informational banner on unsupported browsers
8. THE React frontend SHALL respect the user's `prefers-reduced-motion` setting and SHALL suppress non-essential motion (transitions, animated transitions between routes) when reduced motion is requested

### Requirement 21: Availability, Backup, and Disaster Recovery

**User Story:** As a technical lead, I want defined availability and recovery targets, so that the business can withstand outages and data loss within agreed limits.

#### Acceptance Criteria

1. THE Loyalty_System SHALL target a monthly availability of 99.5% for the production API and frontend, measured as successful responses to synthetic health checks over the calendar month, excluding scheduled maintenance windows announced at least 7 days in advance
2. THE Loyalty_System SHALL enable DynamoDB point-in-time recovery (PITR) on all production tables with a 35-day recovery window
3. THE Loyalty_System SHALL define a Recovery Time Objective (RTO) of 4 hours and a Recovery Point Objective (RPO) of 1 hour for the production environment, and SHALL document the runbook required to meet these objectives
4. THE Loyalty_System SHALL replicate critical DynamoDB tables (Customer_Profile, Transactions, Redemptions, Audit, Configuration, Gift_Catalog, Gift_Inventory, Outlet registry, and RBAC policy store) to a secondary AWS region using global tables or scheduled exports such that the RPO can be met if the primary region becomes unavailable
5. WHEN a CloudWatch alarm fires for sustained API error rate above 5% over 10 minutes or sustained 5xx responses above 1% over 5 minutes, THE Loyalty_System SHALL notify the on-call administrator via email and the configured incident channel within 5 minutes of the alarm transitioning to ALARM state
6. THE Loyalty_System SHALL perform an automated disaster-recovery drill at least once per calendar quarter, owned by the Technical_Lead, that restores the most recent PITR snapshot to a non-production environment, executes a documented validation test suite, and records the drill outcome (success or failure with reasons) in an operations report shared with the Super_Admin role
7. THE Loyalty_System SHALL retain operational backups for a minimum of 35 days and archived backups for the duration of the Loyalty_Cycle retention period configured in Requirement 3 AC3
8. THE Loyalty_System SHALL deploy all production workloads (Lambda, DynamoDB, S3 buckets holding PII or transaction data, Cognito user pool) in the AWS_Primary_Region (`ap-south-1`, Mumbai), and SHALL restrict cross-region replication per AC4 to AWS regions explicitly approved by the Technical_Lead and Business_Owner, with the approved region list recorded in the Configuration_Service

### Requirement 22: Gift Catalog Management

**User Story:** As an administrator, I want to manage the catalogue of physical gift items independently of per-outlet stock, so that Purchase_Threshold tiers and Gift_Inventory_Service entries reference a single authoritative gift definition.

#### Acceptance Criteria

1. THE Configuration_Service SHALL maintain a Gift_Catalog containing one Gift_Catalog_Item per physical gift, where each item records a unique SKU (non-empty string, maximum 64 characters), display name (non-empty string, maximum 200 characters), description (maximum 1,000 characters), image reference (S3 object key, optional), monetary value in System_Currency (between 0.01 and 999,999.99), and status (active or archived)
2. THE Configuration_Service SHALL allow Admin and Super_Admin roles to create, update, and archive Gift_Catalog_Item entries, and SHALL record every catalogue mutation in the Audit_Service including the administrator identifier, action type, and before/after values
3. IF an administrator attempts to create a Gift_Catalog_Item with a SKU that already exists, THEN THE Configuration_Service SHALL reject the operation and display a message indicating the duplicate SKU
4. WHERE a Purchase_Threshold tier is configured with Gift_Type set to Gift_Item, THE Configuration_Service SHALL require the administrator to select a Gift_Catalog_Item in active status from the Gift_Catalog (replacing the implicit catalogue reference in Requirement 4 AC10)
5. IF an administrator attempts to archive a Gift_Catalog_Item that is currently referenced by any enabled Purchase_Threshold tier, THEN THE Configuration_Service SHALL reject the archival and display a message listing the referencing Purchase_Threshold tiers
6. WHEN a Gift_Catalog_Item is archived, THE Gift_Inventory_Service SHALL prevent new stock additions and new transfers for that item but SHALL continue to permit redemption of existing stock until depleted
7. THE Loyalty_System SHALL display the referenced Gift_Catalog_Item's display name, image, and monetary value on all customer-facing notifications, redemption screens, and reports that reference a Gift_Item redemption

### Requirement 23: Customer Identity Resolution

**User Story:** As a data steward, I want a single, deterministic rule for resolving customer identity across ingestion sources, so that purchase history and loyalty progression are attributed to the correct Customer_Profile.

#### Acceptance Criteria

1. THE Loyalty_System SHALL treat the Customer_Identity_Key (the Valid_Phone_Number) as the natural key for Customer_Profile resolution across the External API ingestion (Requirement 1), Excel import (Requirement 2), and in-store enrolment
2. WHEN an incoming transaction record contains a phone number that matches an existing Customer_Profile under the Customer_Identity_Key, THE Loyalty_System SHALL attach the transaction to that Customer_Profile regardless of whether the source-supplied customer identifier matches the existing customer identifier on the profile
3. WHEN an incoming transaction record contains a phone number that does not match any existing Customer_Profile, THE Loyalty_System SHALL create a new Customer_Profile using the normalised Valid_Phone_Number as the Customer_Identity_Key and assign a system-generated internal customer identifier
4. IF an incoming transaction record contains a customer identifier that matches an existing Customer_Profile but a different Valid_Phone_Number, THEN THE Loyalty_System SHALL flag the record as an identity conflict, store it in a quarantine queue without incrementing purchase counts, and notify administrators via the Notification_Service for manual resolution
5. THE Loyalty_System SHALL provide an administrator UI to merge two Customer_Profile records, transferring purchase history, redemption history, exchange history, and Store_Credit_Balance to the surviving profile and marking the merged profile as superseded; the merge SHALL be recorded in the Audit_Service with both source identifiers and the merge timestamp
6. IF an administrator attempts to merge two Customer_Profile records that have overlapping active verification codes for the same Purchase_Threshold tier, THEN THE Loyalty_System SHALL reject the merge and display a message indicating that conflicting active codes must be resolved (redeemed, expired, or reassigned) first
7. WHEN a Customer_Profile is merged, THE Loyalty_System SHALL preserve the Audit_Service records of the superseded profile and SHALL retain a pointer from the superseded profile to the surviving profile for forensic traceability
8. THE Loyalty_System SHALL resolve every quarantined identity-conflict record (AC4) within 14 calendar days of quarantine, and IF a quarantined record remains unresolved after 7 calendar days, THEN THE Notification_Service SHALL escalate to the Business_Owner; IF a quarantined record remains unresolved after 14 calendar days, THEN THE Loyalty_System SHALL flag the record as expired-unresolved, exclude it from all loyalty-progression calculations permanently, and record the expiry in the Audit_Service

### Requirement 24: Test Strategy and Quality Gates

**User Story:** As an engineering lead, I want defined quality gates enforced in the build pipeline, so that regressions in core loyalty workflows are caught before reaching production.

#### Acceptance Criteria

1. THE Loyalty_System build pipeline SHALL require unit test line coverage of at least 80% for backend services (Data_Ingestion_Service, Gift_Redemption_Service, Configuration_Service, RBAC_Service, Audit_Service) measured per pull request, and SHALL fail the build when coverage falls below the threshold
2. THE Loyalty_System build pipeline SHALL execute an integration test suite against a local DynamoDB and mocked AWS services on every pull request, covering at minimum: external API ingestion, Excel import, threshold progression, verification-code issuance, gift redemption (both Gift_Item and Cash_Return), exchange recording, and cycle reset
3. THE Loyalty_System SHALL include an end-to-end test suite executed against a deployed staging environment that covers the complete redemption workflow (eligibility -> SMS issuance -> in-store verification -> Gift_Item or Cash_Return payout) and the Excel import workflow (upload -> async processing -> summary)
4. WHEN the end-to-end test suite fails on the staging environment, THE Loyalty_System deployment pipeline SHALL block promotion to production until the failure is resolved or formally waived by the engineering lead, with the waiver recorded in the Audit_Service
5. THE Loyalty_System SHALL include accessibility automated tests (axe-core or equivalent) covering all primary React frontend routes, and SHALL fail the build on any new violation classified as Serious or Critical per the testing tool's severity scale
6. THE Loyalty_System SHALL execute a load test against a staging environment at least once per release candidate, validating that the API meets the latency and error-rate targets defined in Requirement 13 AC7 and Requirement 21 AC5 under the documented peak load profile
7. THE Loyalty_System SHALL require a documented user acceptance test (UAT) signoff from the Business_Owner before any production release that introduces changes to the Configuration_Service, Gift_Redemption_Service, or Notification_Service, with the signoff recorded in the release tracker
8. THE Loyalty_System SHALL define and document a peak load profile that the load test in AC6 validates against, comprising at minimum: sustained API throughput of 200 requests per second with p95 latency at or below 1 second, peak burst throughput of 500 requests per second for 5 minutes with p95 latency at or below 2 seconds, 1,000 concurrent authenticated user sessions, an Excel import of 500,000 rows completing within the 5-minute target in Requirement 2 AC2, and a transactions table of at least 10,000,000 records; the peak load profile SHALL be reviewed by the Technical_Lead at least once per Loyalty_Cycle and updated when business projections change

### Requirement 25: Observability and Cost Guardrails

**User Story:** As an operations lead, I want comprehensive observability and cost controls, so that I can diagnose incidents quickly and contain runaway AWS spend.

#### Acceptance Criteria

1. THE Loyalty_System SHALL emit distributed traces using AWS X-Ray (or equivalent OpenTelemetry-compatible service) for every API request, propagating the correlation identifier from Requirement 14 AC8 across Lambda invocations, DynamoDB calls, and external service calls (SMS gateway, AWS SES, external ingestion API)
2. THE Loyalty_System SHALL expose business KPI dashboards in CloudWatch (or equivalent) covering at minimum: SMS delivery success rate (last 24 hours), redemption success rate (last 24 hours), ingestion job success rate (last 24 hours), active customer count, and Cash_Return payout failure count
3. WHEN any business KPI defined in AC2 deviates from its configured baseline by more than the configured threshold (default: 20% for delivery and success rates, any non-zero count for Cash_Return payout failures), THE Loyalty_System SHALL trigger a CloudWatch alarm and notify the on-call administrator within 5 minutes
4. THE Loyalty_System SHALL configure an AWS Budgets monthly cost guardrail for the production account with a configured budget amount, and IF actual or forecasted spend exceeds 80% of the budget, THEN AWS Budgets SHALL notify the Technical_Lead and Finance_Team via email
5. THE Loyalty_System SHALL configure an AWS CloudWatch billing anomaly alarm that fires on a daily spend deviation greater than 50% from the trailing 7-day average, with notification delivered to the Technical_Lead within 1 hour
6. THE Loyalty_System SHALL expose synthetic uptime probes from at least two AWS regions targeting the API health endpoint and the React frontend, with probes executed every 5 minutes, and SHALL feed the probe results into the availability measurement defined in Requirement 21 AC1
7. THE Loyalty_System SHALL retain CloudWatch metrics for at least 15 months to support year-over-year analysis aligned with the Loyalty_Cycle
