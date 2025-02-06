import axios from 'axios';

// Print Environment Variables (for debugging)
console.log('All Environment Variables:', import.meta.env);
console.log('Mode:', import.meta.env.MODE);
console.log('API_BASE_URL:', import.meta.env.VITE_API_BASE_URL);

// Create a reusable Axios instance
const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || '', // Fallback to '' if not defined
});

/**
 * Health Check
 * GET /api/health
 * @returns {Promise<any>} Response data from health check
 */
export const healthCheck = async () => {
  try {
    const response = await apiClient.get('/api/health');
    return response.data;
  } catch (error) {
    console.error('Error checking health:', error);
    throw error;
  }
};

/**
 * Upload raw image directory (as a zip file)
 * POST /api/image/raw
 * @param {string} timeStamp
 * @param {string} projectName
 * @param {File} directoryFile
 * @returns {Promise<any>} Success message or extracted files
 */
export const uploadRawDirectory = async (timeStamp, projectName, directoryFile) => {
  const formData = new FormData();
  formData.append('directoryFile', directoryFile);

  try {
    const response = await apiClient.post('/api/image/raw', formData, {
      params: { timeStamp, projectName },
      headers: {
        'Content-Type': 'multipart/form-data',
      },
    });
    return response.data;
  } catch (error) {
    if (error.response) {
      console.error('Error response:', error.response);
      console.error('Error response data:', error.response.data);
      console.error('Error status:', error.response.status);
    } else if (error.request) {
      console.error('Error request:', error.request);
    } else {
      console.error('Error message:', error.message);
    }
    throw error;
  }
};

/**
 * Upload processed image directory (as a zip file)
 * POST /api/image/processed
 * @param {string} timeStamp
 * @param {string} projectName
 * @param {string} rawfileDirectoryName
 * @param {File} directoryFile
 * @returns {Promise<any>} Success message or extracted files
 */
export const uploadProcessedDirectory = async (timeStamp, projectName, rawfileDirectoryName, directoryFile) => {
  const formData = new FormData();
  formData.append('directoryFile', directoryFile);

  try {
    const response = await apiClient.post('/api/image/processed', formData, {
      params: { timeStamp, projectName, rawfileDirectoryName },
      headers: {
        'Content-Type': 'multipart/form-data',
      },
    });
    return response.data;
  } catch (error) {
    console.error('Error uploading processed directory:', error);
    throw error;
  }
};

/**
 * Delete a project and its associated files
 * DELETE /api/image/projects
 * @param {string} projectName
 * @param {string} timestamp
 * @returns {Promise<any>} Success message
 */
export const deleteProject = async (projectName, timestamp) => {
  try {
    const response = await apiClient.delete('/api/image/projects', {
      params: { projectName, timestamp },
    });
    return response.data;
  } catch (error) {
    console.error('Error deleting project:', error);
    throw error;
  }
};

/**
 * Get a list of projects
 * GET /api/image/projects
 * @param {number} [year]
 * @param {string} [projectName]
 * @param {string} [timestamp]
 * @returns {Promise<any>} List of projects
 */
export const getProjects = async (year = null, projectName = null, timestamp = null) => {
  try {
    const response = await apiClient.get('/api/image/projects', {
      params: { year, projectName, timestamp },
    });
    return response.data;
  } catch (error) {
    console.error('Error fetching projects:', error);
    throw error;
  }
};

