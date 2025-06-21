#!/usr/bin/env node

/**
 * Simple JWT Token Generator for AzurePhotoFlow Development
 * 
 * This script creates a test JWT token that can be used with Swagger
 * for development and testing purposes.
 */

const crypto = require('crypto');

// Configuration (matches your backend JWT settings)
const JWT_SECRET = process.env.JWT_SECRET_KEY || 'your-secret-key-here';
const ISSUER = 'loicportraits.azurewebsites.net';
const AUDIENCE = 'loicportraits.azurewebsites.net';

function base64UrlEncode(data) {
    return Buffer.from(data)
        .toString('base64')
        .replace(/\+/g, '-')
        .replace(/\//g, '_')
        .replace(/=/g, '');
}

function createTestJWT(userId = 'test-user', email = 'test@example.com') {
    const header = {
        alg: 'HS256',
        typ: 'JWT'
    };

    const payload = {
        sub: userId,
        email: email,
        role: 'FullAccess',
        iss: ISSUER,
        aud: AUDIENCE,
        exp: Math.floor(Date.now() / 1000) + (7 * 24 * 60 * 60), // 7 days
        iat: Math.floor(Date.now() / 1000)
    };

    const encodedHeader = base64UrlEncode(JSON.stringify(header));
    const encodedPayload = base64UrlEncode(JSON.stringify(payload));
    
    const signature = crypto
        .createHmac('sha256', JWT_SECRET)
        .update(`${encodedHeader}.${encodedPayload}`)
        .digest('base64')
        .replace(/\+/g, '-')
        .replace(/\//g, '_')
        .replace(/=/g, '');

    return `${encodedHeader}.${encodedPayload}.${signature}`;
}

// Check if JWT_SECRET_KEY is set
if (!process.env.JWT_SECRET_KEY) {
    console.log('⚠️  Warning: JWT_SECRET_KEY environment variable not set.');
    console.log('   Using default secret. Set JWT_SECRET_KEY for production use.');
    console.log('');
}

const token = createTestJWT();

console.log('🔑 Test JWT Token for AzurePhotoFlow Swagger Authentication');
console.log('');
console.log('Token:');
console.log(token);
console.log('');
console.log('📋 How to use in Swagger:');
console.log('1. Go to http://localhost/swagger');
console.log('2. Click the "Authorize" button (🔒 icon)');
console.log('3. In the "Value" field, enter: Bearer ' + token);
console.log('4. Click "Authorize"');
console.log('5. Click "Close"');
console.log('');
console.log('✅ You can now test authenticated endpoints in Swagger!');
console.log('');
console.log('📝 Note: This token is valid for 7 days and contains:');
console.log('   - User ID: test-user');
console.log('   - Email: test@example.com');
console.log('   - Role: FullAccess');