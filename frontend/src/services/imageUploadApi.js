import axios from 'axios';

// print all Environment Variables
console.log('All Environment Variables:', import.meta.env);

console.log('Mode:', import.meta.env.MODE);
const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;
console.log('API_BASE_URL:', API_BASE_URL);

// Upload raw image directory as a zip file
// Endpoint: POST /api/image/raw
// Parameters: timeStamp, projectName, directoryFile
export const uploadRawDirectory = async (timeStamp, projectName, directoryFile) => {
  const formData = new FormData();
  formData.append('directoryFile', directoryFile);

  try {
    const response = await axios.post(`/api/image/raw`, formData, {
      params: { timeStamp, projectName },
      headers: {
        'Content-Type': 'multipart/form-data',
      },
    });
    return response.data; // Returns the extracted files or success message
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
  throw error;}
};

// Upload processed image directory as a zip file
// Endpoint: POST /api/image/processed
// Parameters: timeStamp, projectName, rawfileDirectoryName, directoryFile
// rawfileDirectoryName is the name of the raw file directory that was uploaded
// directoryFile is the zip file containing the processed images
// The processed images will be stored in the directory with the same name as the raw file directory
export const uploadProcessedDirectory = async (timeStamp, projectName, rawfileDirectoryName, directoryFile) => {
  const formData = new FormData();
  formData.append('directoryFile', directoryFile);

  try {
    const response = await axios.post(`$/api/image/processed`, formData, {
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
// Endpoint: DELETE /api/image/projects
// Parameters: projectName, timestamp
// projectName is the name of the project to delete
// timestamp is the timestamp of the project to delete
// The project directory and all associated files will be deleted
// Returns a success message
export const deleteProject = async (projectName, timestamp) => {
  try {
    const response = await axios.delete(`$/api/image/projects`, {
      params: { projectName, timestamp },
    });
    return response.data; // Returns success message
  } catch (error) {
    console.error('Error deleting project:', error);
    throw error;
  }
};

// Get a list of projects
// Endpoint: GET /api/image/projects
// Parameters: year, projectName, timestamp
// year is the year of the project
// projectName is the name of the project
// timestamp is the timestamp of the project
// Returns a list of projects
export const getProjects = async (year = null, projectName = null, timestamp = null) => {
  try {
    const response = await axios.get(`$/api/image/projects`, {
      params: { year, projectName, timestamp },
    });
    return response.data; // Returns a list of projects
  } catch (error) {
    console.error('Error fetching projects:', error);
    throw error;
  }
};
