#!/usr/bin/env pwsh

Write-Host "🐳 Starting Docker-based Integration Tests..." -ForegroundColor Cyan

# Function to cleanup on exit
function Cleanup {
    Write-Host "🧹 Cleaning up test containers..." -ForegroundColor Yellow
    docker-compose -f docker-compose.test.yml down -v 2>$null
}

# Register cleanup function
Register-EngineEvent -SourceIdentifier PowerShell.Exiting -Action { Cleanup }

try {
    # Clean up any existing test containers
    Write-Host "🧹 Cleaning up any existing test containers..." -ForegroundColor Yellow
    docker-compose -f docker-compose.test.yml down -v 2>$null

    # Start the test database
    Write-Host "🚀 Starting test database container..." -ForegroundColor Green
    docker-compose -f docker-compose.test.yml up -d test-db

    # Wait for database to be healthy
    Write-Host "⏳ Waiting for database to be ready..." -ForegroundColor Yellow
    $timeout = 120  # Increased timeout for SQL Server startup
    $elapsed = 0
    $ready = $false

    while ($elapsed -lt $timeout -and -not $ready) {
        try {
            # Check if container is running
            $containerStatus = docker inspect sqlserver-test --format '{{.State.Status}}' 2>$null
            if ($containerStatus -eq "running") {
                # Test database connection using the new sqlcmd
                docker exec sqlserver-test bash -c "timeout 5s bash -c '</dev/tcp/localhost/1433'" 2>$null | Out-Null
                if ($LASTEXITCODE -eq 0) {
                    Write-Host "✅ Database is ready!" -ForegroundColor Green
                    $ready = $true
                    break
                }
            } else {
                Write-Host "💭 Container status: $containerStatus" -ForegroundColor Gray
            }
        }
        catch {
            # Ignore errors and continue waiting
        }
        
        if ($elapsed % 10 -eq 0) {
            Write-Host "💭 Still waiting... (${elapsed}s/${timeout}s)" -ForegroundColor Gray
        }
        
        Start-Sleep -Seconds 2
        $elapsed += 2
    }

    if (-not $ready) {
        Write-Host "❌ Database failed to start within $timeout seconds" -ForegroundColor Red
        Write-Host "📋 Container logs:" -ForegroundColor Yellow
        docker logs sqlserver-test
        Write-Host "📋 Container status:" -ForegroundColor Yellow
        docker ps -a --filter "name=sqlserver-test"
        exit 1
    }

    # Initialize the database
    Write-Host "📊 Initializing test database..." -ForegroundColor Cyan
    docker-compose -f docker-compose.test.yml up test-db-init
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Database initialization failed!" -ForegroundColor Red
        docker logs sqlserver-test
        exit 1
    }

    # Verify database setup
    Write-Host "🔍 Verifying database setup..." -ForegroundColor Cyan
    docker run --rm --network riva-test_test-network mcr.microsoft.com/mssql-tools18 /opt/mssql-tools18/bin/sqlcmd -S test-db -U sa -P TestPassword123! -C -d ContactManagerTest -Q "SELECT COUNT(*) as ContactCount FROM Contacts"

    # Run the integration tests
    Write-Host "🧪 Running integration tests..." -ForegroundColor Cyan
    Set-Location server
    dotnet test ContactManager.IntegrationTests --verbosity normal --logger console
    $testExitCode = $LASTEXITCODE

    # Return to root directory
    Set-Location ..

    if ($testExitCode -eq 0) {
        Write-Host "✅ All integration tests passed!" -ForegroundColor Green
    } else {
        Write-Host "❌ Some integration tests failed!" -ForegroundColor Red
        Write-Host "💡 Check the test output above for details" -ForegroundColor Yellow
    }

    exit $testExitCode
}
catch {
    Write-Host "💥 An error occurred: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
finally {
    Cleanup
}
