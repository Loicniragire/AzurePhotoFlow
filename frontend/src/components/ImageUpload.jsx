import { useState } from 'react';
import axios from 'axios';
import '../styles/ImageUpload.css';

const ImageUpload = () => {
    const [selectedFile, setSelectedFile] = useState(null);
    const [uploadProgress, setUploadProgress] = useState(0);
    const [uploadStatus, setUploadStatus] = useState('');

    const handleFileChange = (event) => {
        setSelectedFile(event.target.files[0]);
        setUploadStatus('');
        setUploadProgress(0);
    };

    const handleUpload = async () => {
        if (!selectedFile) {
            alert('Please select a file to upload.');
            return;
        }

        const formData = new FormData();
        formData.append('file', selectedFile);

        try {
            const response = await axios.post('/api/upload', formData, {
                headers: {
                    'Content-Type': 'multipart/form-data',
                },
                onUploadProgress: (progressEvent) => {
                    const percentCompleted = Math.round((progressEvent.loaded * 100) / progressEvent.total);
                    setUploadProgress(percentCompleted);
                },
            });

            setUploadStatus('Upload successful!');
            console.log('Upload response:', response.data);
        } catch (error) {
            setUploadStatus('Upload failed. Please try again.');
            console.error('Upload error:', error);
        }
    };

    return (
        <div className="image-upload">
            <h2>Upload Your Image</h2>
            <input type="file" accept="image/*" onChange={handleFileChange} />
            <button onClick={handleUpload}>Upload</button>

            {uploadProgress > 0 && (
                <div className="progress-bar">
                    <div
                        className="progress"
                        style={{ width: `${uploadProgress}%` }}
                    >
                        {uploadProgress}%
                    </div>
                </div>
            )}

            {uploadStatus && <p className="status-message">{uploadStatus}</p>}
        </div>
    );
};

export default ImageUpload;

