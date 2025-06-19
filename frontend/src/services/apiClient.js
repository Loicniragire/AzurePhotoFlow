import axios from 'axios';

// Get base URL from environment or use default
const BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost';

// Create axios instance with default configuration
const apiClient = axios.create({
    baseURL: BASE_URL,
    timeout: 30000,
    headers: {
        'Content-Type': 'application/json',
    },
});

// Request interceptor to add JWT token
apiClient.interceptors.request.use(
    (config) => {
        const token = localStorage.getItem('jwtToken');
        if (token) {
            config.headers.Authorization = `Bearer ${token}`;
        }
        return config;
    },
    (error) => {
        return Promise.reject(error);
    }
);

// Response interceptor for error handling
apiClient.interceptors.response.use(
    (response) => {
        return response;
    },
    (error) => {
        if (error.response) {
            // Server responded with error status
            const { status, data } = error.response;
            
            switch (status) {
                case 401:
                    // Unauthorized - clear token and redirect to login
                    localStorage.removeItem('jwtToken');
                    window.location.href = '/login';
                    break;
                case 403:
                    console.error('Forbidden: Insufficient permissions');
                    break;
                case 404:
                    console.error('Not Found: Endpoint does not exist');
                    break;
                case 500:
                    console.error('Server Error: Internal server error');
                    break;
                default:
                    console.error(`API Error ${status}:`, data?.message || error.message);
            }
            
            // Transform error for consistent handling
            const apiError = new Error(data?.message || 'API request failed');
            apiError.status = status;
            apiError.data = data;
            throw apiError;
        } else if (error.request) {
            // Network error
            console.error('Network Error: No response received');
            throw new Error('Network error - please check your connection');
        } else {
            // Request configuration error
            console.error('Request Error:', error.message);
            throw error;
        }
    }
);

export default apiClient;