import { useState } from 'react';
import axios from 'axios';
import '../styles/ImageUpload.css';
import {uploadImage} from '../services/api';

const ImageUpload = () => {
    const [selectedFile, setSelectedFile] = useState(null);
    const [imagePreview, setImagePreview] = useState(null);
    const [metadata, setMetadata] = useState({ title: '', description: '', tags: '' });
    const [uploadProgress, setUploadProgress] = useState(0);
    const [uploadStatus, setUploadStatus] = useState('');

    const handleFileChange = (event) => {
        const file = event.target.files[0];
        if (file) {
            setSelectedFile(file);
            setUploadStatus('');
            setUploadProgress(0);

            // Set default title to match the filename (excluding the extension)
            const defaultTitle = file.name.split('.').slice(0, -1).join('.');
            setMetadata((prev) => ({ ...prev, title: defaultTitle }));

            // Generate image preview
            const reader = new FileReader();
            reader.onload = () => setImagePreview(reader.result);
            reader.readAsDataURL(file);
        }
    };

    const handleMetadataChange = (event) => {
        const { name, value } = event.target;
        setMetadata((prev) => ({ ...prev, [name]: value }));
    };

    const handleUpload = async () => {
        if (!selectedFile) {
            alert('Please select a file to upload.');
            return;
        }

        const formData = new FormData();
        formData.append('file', selectedFile);
        formData.append('metadata', JSON.stringify(metadata));

        try {
            const response = await axios.post('/api/upload', formData, {
                headers: { 'Content-Type': 'multipart/form-data' },
                onUploadProgress: (progressEvent) => {
                    const percentCompleted = Math.round((progressEvent.loaded * 100) / progressEvent.total);
                    setUploadProgress(percentCompleted);
                },
            });

            setUploadStatus('Upload successful!');
			const result = await uploadImage(selectedFile);
			setUploadStatus(`Upload successful: ${result.fileName}`);
            console.log('Upload response:', response.data);
        } catch (error) {
            setUploadStatus('Upload failed. Please try again.');
            console.error('Upload error:', error);
        }
    };

    return (
        <div className="image-upload">
            <h2>Upload Your Image</h2>
            <label htmlFor="file-input" className="file-label">
                {selectedFile ? selectedFile.name : 'Choose an image or drag and drop'}
            </label>
            <input id="file-input" type="file" accept="image/*" onChange={handleFileChange} style={{ display: 'none' }} />

            {imagePreview ? (
                <div className="image-preview">
                    <img src={imagePreview} alt="Preview" />
                </div>
            ) : (
                <div className="image-placeholder">
                    <p>No image selected</p>
                </div>
            )}

            {selectedFile && (
                <div className="metadata-form">
                    <h3>Image Metadata</h3>
                    <input
                        type="text"
                        name="title"
                        placeholder="Title"
                        value={metadata.title}
                        onChange={handleMetadataChange}
                    />
                    <textarea
                        name="description"
                        placeholder="Description"
                        value={metadata.description}
                        onChange={handleMetadataChange}
                    ></textarea>
                    <input
                        type="text"
                        name="tags"
                        placeholder="Tags (comma-separated)"
                        value={metadata.tags}
                        onChange={handleMetadataChange}
                    />
                </div>
            )}

            <button onClick={handleUpload}>Upload</button>

            {uploadProgress > 0 && (
                <div className="progress-bar">
                    <div className="progress" style={{ width: `${uploadProgress}%` }}>
                        {uploadProgress}%
                    </div>
                </div>
            )}

            {uploadStatus && <p className="status-message">{uploadStatus}</p>}
        </div>
    );
};

export default ImageUpload;

