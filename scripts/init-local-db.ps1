# Initialize DynamoDB Local with the VELoyalty table and seed data
$endpoint = "http://localhost:8000"

Write-Host "Creating VELoyalty table..." -ForegroundColor Cyan

# Create the table
aws dynamodb create-table `
    --table-name VELoyalty `
    --attribute-definitions `
        AttributeName=PK,AttributeType=S `
        AttributeName=SK,AttributeType=S `
        AttributeName=GSI1PK,AttributeType=S `
        AttributeName=GSI1SK,AttributeType=S `
        AttributeName=GSI2PK,AttributeType=S `
        AttributeName=GSI2SK,AttributeType=S `
    --key-schema `
        AttributeName=PK,KeyType=HASH `
        AttributeName=SK,KeyType=RANGE `
    --global-secondary-indexes `
        "[{\"IndexName\":\"GSI1\",\"KeySchema\":[{\"AttributeName\":\"GSI1PK\",\"KeyType\":\"HASH\"},{\"AttributeName\":\"GSI1SK\",\"KeyType\":\"RANGE\"}],\"Projection\":{\"ProjectionType\":\"ALL\"}},{\"IndexName\":\"GSI2\",\"KeySchema\":[{\"AttributeName\":\"GSI2PK\",\"KeyType\":\"HASH\"},{\"AttributeName\":\"GSI2SK\",\"KeyType\":\"RANGE\"}],\"Projection\":{\"ProjectionType\":\"ALL\"}}]" `
    --billing-mode PAY_PER_REQUEST `
    --endpoint-url $endpoint `
    --region ap-south-1 2>$null

if ($LASTEXITCODE -eq 0) {
    Write-Host "Table created successfully!" -ForegroundColor Green
} else {
    Write-Host "Table may already exist, continuing..." -ForegroundColor Yellow
}

# Seed admin user (password: Admin123!)
# bcrypt hash for "Admin123!" with cost factor 12
$passwordHash = '$2a$12$LJ3m4sMKfRzlTBhPO3NXxOQzVlVf0wVJXGBl5OQHK8xKZqKZqKZq'

Write-Host "Seeding admin user (admin@veloyalty.com / Admin123!)..." -ForegroundColor Cyan

aws dynamodb put-item `
    --table-name VELoyalty `
    --item "{\"PK\":{\"S\":\"USER#admin-001\"},\"SK\":{\"S\":\"META\"},\"GSI1PK\":{\"S\":\"GSI1_USER\"},\"GSI1SK\":{\"S\":\"USER#admin@veloyalty.com\"},\"userId\":{\"S\":\"admin-001\"},\"email\":{\"S\":\"admin@veloyalty.com\"},\"name\":{\"S\":\"System Admin\"},\"passwordHash\":{\"S\":\"$passwordHash\"},\"role\":{\"S\":\"Admin\"},\"isActive\":{\"BOOL\":true},\"createdAt\":{\"S\":\"2024-01-01T00:00:00Z\"},\"updatedAt\":{\"S\":\"2024-01-01T00:00:00Z\"}}" `
    --endpoint-url $endpoint `
    --region ap-south-1

# Seed a default outlet
Write-Host "Seeding default outlet..." -ForegroundColor Cyan

aws dynamodb put-item `
    --table-name VELoyalty `
    --item "{\"PK\":{\"S\":\"OUTLET#OTL-001\"},\"SK\":{\"S\":\"META\"},\"GSI1PK\":{\"S\":\"GSI1_OUTLET\"},\"GSI1SK\":{\"S\":\"OUTLET#OTL-001\"},\"outletId\":{\"S\":\"OTL-001\"},\"name\":{\"S\":\"Vision Emporium - Gulshan\"},\"address\":{\"S\":\"Gulshan-2, Dhaka\"},\"phoneNumber\":{\"S\":\"+8801711000001\"},\"assignedManagerId\":{\"S\":\"admin-001\"},\"isActive\":{\"BOOL\":true}}" `
    --endpoint-url $endpoint `
    --region ap-south-1

# Seed active loyalty cycle
Write-Host "Seeding active loyalty cycle..." -ForegroundColor Cyan

aws dynamodb put-item `
    --table-name VELoyalty `
    --item "{\"PK\":{\"S\":\"CONFIG\"},\"SK\":{\"S\":\"CYCLE#2025-2026\"},\"CycleId\":{\"S\":\"2025-2026\"},\"StartDate\":{\"S\":\"2025-06-01\"},\"EndDate\":{\"S\":\"2026-05-31\"},\"IsActive\":{\"BOOL\":true}}" `
    --endpoint-url $endpoint `
    --region ap-south-1

# Seed general config
Write-Host "Seeding general configuration..." -ForegroundColor Cyan

aws dynamodb put-item `
    --table-name VELoyalty `
    --item "{\"PK\":{\"S\":\"CONFIG\"},\"SK\":{\"S\":\"SETTINGS#GENERAL\"},\"SyncIntervalMinutes\":{\"N\":\"60\"},\"CodeExpiryDays\":{\"N\":\"30\"},\"MinPurchaseAmount\":{\"N\":\"100\"},\"ExcludedCategories\":{\"L\":[]}}" `
    --endpoint-url $endpoint `
    --region ap-south-1

Write-Host "" -ForegroundColor White
Write-Host "Local database initialized!" -ForegroundColor Green
Write-Host "Admin credentials: admin@veloyalty.com / Admin123!" -ForegroundColor Yellow
