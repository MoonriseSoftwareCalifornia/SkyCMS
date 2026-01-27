# SkyCMS Search Demo Startup Script
# This script starts a project and tests the search endpoints

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("api", "publisher", "editor")]
    [string]$Project = "api"
)

# Configuration for each project
$projects = @{
    "api" = @{
        Path = "Sky.Api"
        Port = 5000
        Name = "Sky.Api (API-only)"
        Description = "RESTful API endpoints for search functionality"
    }
    "publisher" = @{
        Path = "Publisher" 
        Port = 5001
        Name = "Sky.Publisher (Public Site)"
        Description = "Public-facing website with search functionality"
    }
    "editor" = @{
        Path = "Editor"
        Port = 5002  
        Name = "Sky.Editor (Admin Interface)"
        Description = "Administrative interface with advanced search features"
    }
}

$config = $projects[$Project]
$baseUrl = "http://localhost:$($config.Port)"

Write-Host "=== SkyCMS Search Demo ===" -ForegroundColor Green
Write-Host "Starting: $($config.Name)" -ForegroundColor Yellow
Write-Host "Description: $($config.Description)" -ForegroundColor Gray
Write-Host "URL: $baseUrl" -ForegroundColor Cyan
Write-Host ""

# Check if project builds successfully
Write-Host "Building project..." -ForegroundColor Yellow
$buildResult = dotnet build $config.Path
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build failed! Please check the error messages above." -ForegroundColor Red
    exit 1
}

Write-Host "✅ Build successful!" -ForegroundColor Green
Write-Host ""

# Start the project in the background
Write-Host "Starting $($config.Name) on port $($config.Port)..." -ForegroundColor Yellow
$job = Start-Job -ScriptBlock {
    param($projectPath, $port)
    Set-Location $using:PWD
    dotnet run --project $projectPath --urls="http://localhost:$port"
} -ArgumentList $config.Path, $config.Port

# Wait for the application to start
Write-Host "Waiting for application to start..." -ForegroundColor Yellow
$maxAttempts = 30
$attempt = 0

do {
    Start-Sleep -Seconds 1
    $attempt++
    
    try {
        $response = Invoke-WebRequest -Uri "$baseUrl/_api/search/health" -UseBasicParsing -TimeoutSec 2
        if ($response.StatusCode -eq 200) {
            Write-Host "✅ Application started successfully!" -ForegroundColor Green
            break
        }
    } catch {
        if ($attempt -eq $maxAttempts) {
            Write-Host "❌ Failed to start application after $maxAttempts attempts" -ForegroundColor Red
            Stop-Job $job
            Remove-Job $job
            exit 1
        }
        Write-Host "." -NoNewline -ForegroundColor Gray
    }
} while ($attempt -lt $maxAttempts)

Write-Host ""
Write-Host "=== Testing Search Endpoints ===" -ForegroundColor Green

# Test functions
function Test-Endpoint {
    param([string]$Url, [string]$Description)
    
    Write-Host "`n🔍 Testing: $Description" -ForegroundColor Cyan
    
    try {
        $response = Invoke-RestMethod -Uri $Url -Method GET -TimeoutSec 10
        Write-Host "✅ SUCCESS" -ForegroundColor Green
        
        # Show sample response
        if ($response -is [PSCustomObject]) {
            $json = $response | ConvertTo-Json -Depth 2 -Compress
            if ($json.Length -gt 200) {
                $json = $json.Substring(0, 200) + "..."
            }
            Write-Host "   Response: $json" -ForegroundColor Gray
        }
        return $true
    } catch {
        Write-Host "❌ FAILED: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

# Run tests
$tests = @(
    @{ Url = "$baseUrl/_api/search/health"; Description = "Health Check" },
    @{ Url = "$baseUrl/_api/search?query="; Description = "Empty Search (All Results)" },
    @{ Url = "$baseUrl/_api/search?query=test"; Description = "Basic Search" },
    @{ Url = "$baseUrl/_api/search?query=test&page=1&pageSize=5"; Description = "Paginated Search" },
    @{ Url = "$baseUrl/_api/search/suggestions?term=test"; Description = "Search Suggestions" }
)

$passedTests = 0
foreach ($test in $tests) {
    if (Test-Endpoint $test.Url $test.Description) {
        $passedTests++
    }
}

Write-Host ""
Write-Host "=== Test Results ===" -ForegroundColor Green  
Write-Host "Passed: $passedTests / $($tests.Count)" -ForegroundColor $(if ($passedTests -eq $tests.Count) { "Green" } else { "Yellow" })

if ($passedTests -eq $tests.Count) {
    Write-Host "🎉 All tests passed! Your search implementation is working!" -ForegroundColor Green
} else {
    Write-Host "⚠️  Some tests failed. Check the error messages above." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "=== Interactive Testing ===" -ForegroundColor Green
Write-Host "The application is running at: $baseUrl"
Write-Host ""
Write-Host "Web Interface:"
if ($Project -eq "api") {
    Write-Host "  Swagger UI: $baseUrl/swagger (if enabled)"
    Write-Host "  Direct API: Use curl or Postman to test endpoints"
} else {
    Write-Host "  Search Page: $baseUrl/Search"
    Write-Host "  Home Page: $baseUrl"
}

Write-Host ""
Write-Host "API Endpoints:"
Write-Host "  GET $baseUrl/_api/search?query=YOUR_SEARCH_TERM"
Write-Host "  GET $baseUrl/_api/search/suggestions?term=YOUR_TERM"  
Write-Host "  GET $baseUrl/_api/search/health"

Write-Host ""
Write-Host "=== Sample curl Commands ===" -ForegroundColor Yellow
Write-Host @"
# Health check
curl "$baseUrl/_api/search/health"

# Basic search  
curl "$baseUrl/_api/search?query=programming"

# Search with pagination
curl "$baseUrl/_api/search?query=test&page=1&pageSize=10"

# Get suggestions
curl "$baseUrl/_api/search/suggestions?term=prog&maxResults=5"

# Search with sorting
curl "$baseUrl/_api/search?query=test&sortBy=date"
"@

Write-Host ""
Write-Host "Press Ctrl+C to stop the application" -ForegroundColor Yellow
Write-Host ""

# Keep the application running
try {
    while ($true) {
        Start-Sleep -Seconds 1
        
        # Check if job is still running
        if ($job.State -ne "Running") {
            Write-Host "Application stopped unexpectedly!" -ForegroundColor Red
            break
        }
    }
} finally {
    Write-Host "`nStopping application..." -ForegroundColor Yellow
    Stop-Job $job -ErrorAction SilentlyContinue
    Remove-Job $job -ErrorAction SilentlyContinue
    Write-Host "Application stopped." -ForegroundColor Green
}