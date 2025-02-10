import axios from 'axios';

const authApi = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || '', // Base URL for your API endpoints
});

// Add a request interceptor to automatically include the JWT token in the Authorization header,
// while excluding specific endpoints (e.g., login) that should not receive a token.
authApi.interceptors.request.use(
  (config) => {
    // List endpoints that should not have the token attached.
    const excludedPaths = ['/api/auth/google-login'];

    // If the request URL matches any excluded path, return the config unmodified.
    if (config.url && excludedPaths.some((path) => config.url.includes(path))) {
      return config;
    }

    // Otherwise, attach the token if it exists.
    const token = localStorage.getItem('jwtToken');
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  (error) => Promise.reject(error)
);

/**
 * Login with Google.
 * Sends a POST request to /api/auth/google-login with the provided Google token.
 * On success, stores the JWT in local storage.
 * @param {string} token - The Google authentication token.
 * @returns {Promise<Object>} - The response data containing user details and a success message.
 */
export const googleLogin = async (token) => {
  try {
    // This call will not include the token due to the interceptor exclusion.
    const response = await authApi.post('/api/auth/google-login', { token });
    // Store the returned JWT token in local storage.
    localStorage.setItem('jwtToken', response.data.token);
    return response.data;
  } catch (error) {
    console.error('Error during Google login:', error.response || error);
    throw error;
  }
};

/**
 * Logout.
 * Sends a POST request to /api/auth/logout and clears the token from local storage.
 * @returns {Promise<Object>} - The response data containing the logout message.
 */
export const logout = async () => {
  try {
    // Optionally, clear the token before sending the logout request.
    localStorage.removeItem('jwtToken');
    const response = await authApi.post('/api/auth/logout');
    return response.data;
  } catch (error) {
    console.error('Error during logout:', error.response || error);
    throw error;
  }
};

/**
 * Check Authentication Status.
 * Sends a GET request to /api/auth/check to verify if the user is authenticated.
 * The request automatically includes the JWT token via the interceptor.
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

