# Vision Emporium Loyalty System

A serverless, cloud-native customer loyalty and rewards platform deployed on AWS (primary region: `ap-south-1`). The system tracks customer purchases across retail outlets, identifies customers reaching configurable purchase thresholds, awards gifts (Cash_Return or Gift_Item), and manages outlet-specific SMS-verified gift redemption.

## Features

- **Data Ingestion**: Import sales data via External API or Excel file upload
- **Configurable Loyalty Cycles**: Set custom start/end dates (default: June 1 - May 31)
- **Purchase Thresholds**: Configure gift tiers (default: 3rd and 6th purchases)
- **Gift Types**: Cash_Return (monetary refund/store credit) or Gift_Item (physical product)
- **SMS Verification**: 6-digit verification codes bound to specific outlets
- **Store Credit**: Persistent balance across cycles, auto-applied to purchases
- **Gift Inventory**: Per-outlet stock management with low-stock alerts
- **Bilingual Support**: English and Bangla
- **WCAG 2.2 AA**: Accessible design

## Architecture

- **Compute**: AWS Lambda (.NET 8 AOT)
- **API**: API Gateway REST API
- **Database**: DynamoDB Global Table (single-table design)
- **Auth**: Cognito + JWT with MFA
- **Notifications**: Dual SMS Gateway with failover + SES
- **Observability**: CloudWatch, X-Ray, Synthetic Probes

## Tech Stack

- .NET 8 (Native AOT)
- AWS Lambda
- DynamoDB
- React SPA
- AWS Cognito
- AWS SES

## Getting Started

This is the specification repository. See the design document for detailed architecture and API specifications.

## License

Proprietary - Pran RFL Group