import axios from 'axios';

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;
console.log('API_BASE_URL:', API_BASE_URL);

// Upload raw image directory as a zip file
export const uploadRawDirectory = async (timeStamp, projectName, directoryFile) => {
  const formData = new FormData();
  formData.append('directoryFile', directoryFile);

  try {
    const response = await axios.post(`${API_BASE_URL}/api/image/raw`, formData, {
      params: { timeStamp, projectName },
      headers: {
        'Content-Type': 'multipart/form-data',
      },
    });
    return response.data; // Returns the extracted files or success message
  } catch (error) {
    console.error('Error uploading raw directory:', error);
    throw error;
  }
};

// Upload processed image directory as a zip file
export const uploadProcessedDirectory = async (timeStamp, projectName, rawfileDirectoryName, directoryFile) => {
  const formData = new FormData();
  formData.append('directoryFile', directoryFile);

  try {
    const response = await axios.post(`${API_BASE_URL}/api/image/processed`, formData, {
      params: { timeStamp, projectName, rawfileDirectoryName },
      headers: {
        'Content-Type': 'multipart/form-data',
      },
    });
    return response.data; // Returns the extracted files or success message
  } catch (error) {
    console.error('Error uploading processed directory:', error);
    throw error;
  }
};

// Delete a project and its associated files
export const deleteProject = async (projectName, timestamp) => {
  try {
    const response = await axios.delete(`${API_BASE_URL}/api/image/projects`, {
      params: { projectName, timestamp },
    });
    return response.data; // Returns success message
  } catch (error) {
    console.error('Error deleting project:', error);
    throw error;
  }
};

// Get a list of projects
export const getProjects = async (year = null, projectName = null, timestamp = null) => {
  try {
    const response = await axios.get(`${API_BASE_URL}/api/image/projects`, {
      params: { year, projectName, timestamp },
    });
    return response.data; // Returns a list of projects
  } catch (error) {
    console.error('Error fetching projects:', error);
    throw error;
  }
};

