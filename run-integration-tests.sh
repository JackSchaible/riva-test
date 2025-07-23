#!/bin/bash

echo "🐳 Starting Docker-based Integration Tests..."

# Function to cleanup on exit
cleanup() {
    echo "🧹 Cleaning up test containers..."
    docker-compose -f docker-compose.test.yml down -v
}

# Set trap to cleanup on script exit
trap cleanup EXIT

# Start the test database
echo "🚀 Starting test database container..."
docker-compose -f docker-compose.test.yml up -d test-db

# Wait for database to be healthy
echo "⏳ Waiting for database to be ready..."
timeout=60
elapsed=0
while [ $elapsed -lt $timeout ]; do
    if docker exec sqlserver-test /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P TestPassword123! -Q "SELECT 1" > /dev/null 2>&1; then
        echo "✅ Database is ready!"
        break
    fi
    sleep 2
    elapsed=$((elapsed + 2))
done

if [ $elapsed -ge $timeout ]; then
    echo "❌ Database failed to start within ${timeout} seconds"
    exit 1
fi

# Initialize the database
echo "📊 Initializing test database..."
docker-compose -f docker-compose.test.yml up test-db-init

# Run the integration tests
echo "🧪 Running integration tests..."
cd server
dotnet test ContactManager.IntegrationTests --verbosity normal

# Capture test exit code
test_exit_code=$?

# Cleanup will happen automatically due to trap

if [ $test_exit_code -eq 0 ]; then
    echo "✅ All integration tests passed!"
else
    echo "❌ Some integration tests failed!"
fi

exit $test_exit_code
