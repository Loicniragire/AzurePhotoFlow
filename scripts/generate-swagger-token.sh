#!/bin/bash

# Generate JWT token for Swagger authentication
# This script automatically uses the correct JWT secret from the backend

echo "🔑 Generating Swagger authentication token..."
echo ""

# Get the JWT secret from the running backend container
JWT_SECRET=$(docker exec backend printenv JWT_SECRET_KEY 2>/dev/null)

if [ -z "$JWT_SECRET" ]; then
    echo "❌ Error: Could not get JWT_SECRET_KEY from backend container"
    echo "   Make sure the backend container is running: docker compose up -d"
    exit 1
fi

echo "✅ Retrieved JWT secret from backend container"
echo ""

# Generate the token
JWT_SECRET_KEY="$JWT_SECRET" node scripts/create-test-jwt.js