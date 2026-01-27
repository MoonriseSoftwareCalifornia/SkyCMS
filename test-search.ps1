# SkyCMS Search Testing Script
# Run this script to test all search endpoints

# Colors for output
$Green = "Green"
$Red = "Red" 
$Yellow = "Yellow"

function Test-Endpoint {
    param(
        [string]$Url,
        [string]$Description
    )
    
    Write-Host "`nTesting: $Description" -ForegroundColor $Yellow
    Write-Host "URL: $Url"
    
    try {
        $response = Invoke-RestMethod -Uri $Url -Method GET -TimeoutSec 10
        Write-Host "✓ SUCCESS" -ForegroundColor $Green
        return $response
    }
    catch {
        Write-Host "✗ FAILED: $($_.Exception.Message)" -ForegroundColor $Red
        return $null
    }
}

function Test-SearchEndpoints {
    param([string]$BaseUrl)
    
    Write-Host "=== Testing Search Endpoints at $BaseUrl ===" -ForegroundColor $Yellow
    
    # Test health endpoint
    Test-Endpoint "$BaseUrl/_api/search/health" "Search Health Check"
    
    # Test basic search
    Test-Endpoint "$BaseUrl/_api/search?query=test" "Basic Search Query"
    
    # Test empty search (should return all results)
    Test-Endpoint "$BaseUrl/_api/search?query=" "Empty Search Query"
    
    # Test pagination
    Test-Endpoint "$BaseUrl/_api/search?query=test&page=1&pageSize=5" "Paginated Search"
    
    # Test sorting
    Test-Endpoint "$BaseUrl/_api/search?query=test&sortBy=date" "Search with Date Sort"
    
    # Test suggestions
    Test-Endpoint "$BaseUrl/_api/search/suggestions?term=test&maxResults=5" "Search Suggestions"
}

# Check if any projects are running
Write-Host "SkyCMS Search Endpoint Testing" -ForegroundColor $Green
Write-Host "================================`n"

Write-Host "First, let's check what ports are in use:" -ForegroundColor $Yellow
netstat -an | Select-String "LISTENING" | Select-String ":5[0-9][0-9][0-9]" | Select-Object -First 10

Write-Host "`nTo test the search endpoints, you need to start one of the projects first:"
Write-Host "1. Sky.Api:      dotnet run --project Sky.Api --urls=http://localhost:5000"
Write-Host "2. Sky.Publisher: dotnet run --project Publisher --urls=http://localhost:5001" 
Write-Host "3. Sky.Editor:   dotnet run --project Editor --urls=http://localhost:5002"

# Test common ports
$ports = @(5000, 5001, 5002, 7000, 7001, 7002)

foreach ($port in $ports) {
    $url = "http://localhost:$port"
    
    try {
        $response = Invoke-WebRequest -Uri "$url/_api/search/health" -Method GET -TimeoutSec 3 -UseBasicParsing
        if ($response.StatusCode -eq 200) {
            Write-Host "`nFound running SkyCMS instance at port $port!" -ForegroundColor $Green
            Test-SearchEndpoints $url
        }
    }
    catch {
        # Port not responding, continue to next
    }
}

Write-Host "`n=== Manual Testing Commands ===" -ForegroundColor $Yellow
Write-Host "If you have a project running, use these curl commands:"
Write-Host ""
Write-Host "# Health check"
Write-Host "curl `"http://localhost:5000/_api/search/health`""
Write-Host ""
Write-Host "# Basic search"  
Write-Host "curl `"http://localhost:5000/_api/search?query=test&page=1&pageSize=10`""
Write-Host ""
Write-Host "# Search suggestions"
Write-Host "curl `"http://localhost:5000/_api/search/suggestions?term=test&maxResults=5`""
Write-Host ""
Write-Host "# Test rate limiting (run multiple times quickly)"
Write-Host "for i in {1..35}; do curl `"http://localhost:5000/_api/search?query=test`$i`"; done"

Write-Host "`n=== Database Test Data Setup ===" -ForegroundColor $Yellow
Write-Host "To test with sample data, add some test articles to your database:"
Write-Host @"
INSERT INTO Articles (Title, Content, UrlPath, StatusCode, Published, Updated, VersionNumber)
VALUES 
('Test Article 1', 'This is test content about programming', '/test-1', 0, GETUTCDATE(), GETUTCDATE(), 1),
('Sample Post', 'Content about web development and coding', '/sample-post', 0, GETUTCDATE(), GETUTCDATE(), 1),
('Guide to Testing', 'How to test search functionality effectively', '/testing-guide', 0, GETUTCDATE(), GETUTCDATE(), 1);
"@

Write-Host "`nTesting completed!" -ForegroundColor $Green