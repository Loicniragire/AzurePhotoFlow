import { useState } from 'react';
import JSZip from 'jszip'; // Import JSZip for zipping files
import '../styles/ImageUpload.css';
import { uploadRawDirectory } from '../services/imageUploadApi';
import { formatDate } from '../utils/formatDate';

const ImageUpload = () => {
    const [isZipUpload, setIsZipUpload] = useState(false);
    const [selectedFiles, setSelectedFiles] = useState([]);
    const [zipFile, setZipFile] = useState(null);
    const [uploadProgress, setUploadProgress] = useState(0);
    const [uploadStatus, setUploadStatus] = useState('');
    const [projectName, setProjectName] = useState('');
    const [timeStamp, setTimeStamp] = useState('');

    const handleFileChange = (event) => {
        if (isZipUpload) {
            const file = event.target.files[0];
            if (file && file.type === 'application/zip') {
                setZipFile(file);
                setUploadStatus('');
                setUploadProgress(0);
            } else {
                alert('Please upload a valid .zip file.');
                setZipFile(null);
            }
        } else {
            const files = Array.from(event.target.files);
            if (files.length > 0) {
                setSelectedFiles(files);
                setUploadStatus('');
                setUploadProgress(0);
            }
        }
    };

const handleUpload = async () => {
    // Validate inputs
    if (!projectName.trim()) {
        alert('Project name cannot be empty.');
        return;
    }

    if (!timeStamp) {
        alert('Timestamp is required.');
        return;
    }

    const formattedTimeStamp = formatDate(timeStamp);
    if (!formattedTimeStamp) {
        alert('Invalid timestamp format. Please use yyyy-MM-dd.');
        return;
    }

    let directoryFile = zipFile;

    if (!isZipUpload) {
        if (selectedFiles.length === 0) {
            alert('Please select files to upload.');
            return;
        }

        // Create ZIP file programmatically
        const zip = new JSZip();
        selectedFiles.forEach((file) => {
            zip.file(file.name, file);
        });

        try {
            setUploadStatus('Zipping files...');
            const zipBlob = await zip.generateAsync({ type: 'blob' });
            directoryFile = new File([zipBlob], `${projectName}.zip`, { type: 'application/zip' });
        } catch (error) {
            alert('Error creating ZIP file. Please try again.');
            console.error('Zipping error:', error);
            return;
        }
    }

    try {
        setUploadStatus('Uploading files...');
        const response = await uploadRawDirectory(formattedTimeStamp, projectName, directoryFile);
        setUploadStatus('Upload successful!');
        console.log('Upload response:', response);
    } catch (error) {
        setUploadStatus('Upload failed. Please try again.');
        console.error('Upload error:', error);
    }
};

return (
    <div className="image-upload">
        <h2>Upload Your Files</h2>

        {/* Project Name Input */}
        <input
            type="text"
            placeholder="Enter project name"
            value={projectName}
            onChange={(e) => setProjectName(e.target.value)}
            className="project-name-input"
        />

        {/* Timestamp Input */}
		<input
			type="date"
			placeholder="Enter timestamp (yyyy-MM-dd)"
			value={timeStamp}
			onChange={(e) => {
				const rawDate = e.target.value; 
				console.log('Raw Date Value:', rawDate); // Debugging
				setTimeStamp(rawDate);
			}}
			className="timestamp-input"
		/>

        {/* Upload Mode Toggle */}
        <div className="upload-mode-toggle">
            <label>
                <input
                    type="radio"
                    name="uploadMode"
                    checked={!isZipUpload}
                    onChange={() => setIsZipUpload(false)}
                />
                Upload Individual Files
            </label>
            <label>
                <input
                    type="radio"
                    name="uploadMode"
                    checked={isZipUpload}
                    onChange={() => setIsZipUpload(true)}
                />
                Upload ZIP File
            </label>
        </div>

        {/* File Selection */}
        <label htmlFor="file-input" className="file-label">
            {isZipUpload
                ? zipFile
                    ? zipFile.name
                    : 'Choose a .zip file'
                : selectedFiles.length > 0
                ? `${selectedFiles.length} files selected`
                : 'Choose images or drag and drop'}
        </label>
        <input
            id="file-input"
            type="file"
            accept={isZipUpload ? '.zip' : 'image/*'}
            multiple={!isZipUpload}
            onChange={handleFileChange}
            style={{ display: 'none' }}
        />

        {/* File List for Individual Files */}
        {!isZipUpload && selectedFiles.length > 0 && (
            <ul className="file-list">
                {selectedFiles.map((file, index) => (
                    <li key={index}>{file.name}</li>
                ))}
            </ul>
        )}

        {/* Upload Button */}
        <button onClick={handleUpload}>Upload</button>

        {/* Upload Progress */}
        {uploadProgress > 0 && (
            <div className="progress-bar">
                <div className="progress" style={{ width: `${uploadProgress}%` }}>
                    {uploadProgress}%
                </div>
            </div>
        )}

        {/* Upload Status */}
        {uploadStatus && <p className="status-message">{uploadStatus}</p>}
    </div>
);
};

export default ImageUpload;

