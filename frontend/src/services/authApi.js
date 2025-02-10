import axios from 'axios';

// Create a reusable Axios instance
const authApi = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || '',  // Base URL for your API endpoints
  withCredentials: true,
});

/**
 * Login with Google
 * Sends a POST request to /api/auth/google-login with the provided Google token.
 * @param {string} token - The Google authentication token.
 * @returns {Promise<Object>} - The response data containing user details and a success message.
 */
export const googleLogin = async (token) => {
  try {
    const response = await authApi.post('/api/auth/google-login', { token });
    return response.data;
  } catch (error) {
    console.error('Error during Google login:', error.response || error);
    throw error;
  }
};

/**
 * Logout
 * Sends a POST request to /api/auth/logout to sign the user out.
 * @returns {Promise<Object>} - The response data containing the logout message.
 */
export const logout = async () => {
  try {
    const response = await authApi.post('/api/auth/logout');
    return response.data;
  } catch (error) {
    console.error('Error during logout:', error.response || error);
    throw error;
  }
};

/**
 * Check Authentication Status
 * Sends a GET request to /api/auth/check to verify if the user is authenticated.
 * @returns {Promise<Object>} - The response data indicating authentication status.
 */
export const checkAuthStatus = async () => {
  try {
    const response = await authApi.get('/api/auth/check');
    return response.data;
  } catch (error) {
    console.error('Error checking auth status:', error.response || error);
    throw error;
  }
};

