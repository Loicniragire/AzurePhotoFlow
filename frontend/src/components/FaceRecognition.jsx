import { useState } from 'react';
import PropTypes from 'prop-types';
import '../styles/FaceRecognition.css';

const FaceRecognition = ({ imageUrl, faceData = [] }) => {
    const [selectedFace, setSelectedFace] = useState(null);

    const handleFaceClick = (face) => {
        setSelectedFace(face);
    };

    return (
        <div className="face-recognition">
            <h2>Face Recognition Results</h2>

            {imageUrl ? (
                <div className="image-container">
                    <img src={imageUrl} alt="Analyzed" className="analyzed-image" />
                    {faceData.map((face, index) => (
                        <div
                            key={index}
                            className="face-box"
                            style={{
                                top: `${face.boundingBox.top}px`,
                                left: `${face.boundingBox.left}px`,
                                width: `${face.boundingBox.width}px`,
                                height: `${face.boundingBox.height}px`,
                            }}
                            onClick={() => handleFaceClick(face)}
                        ></div>
                    ))}
                </div>
            ) : (
                <p>No image available for analysis.</p>
            )}

            {selectedFace && (
                <div className="face-details">
                    <h3>Face Details</h3>
                    <ul>
                        <li>Age: {selectedFace.age}</li>
                        <li>Gender: {selectedFace.gender}</li>
                        <li>Emotion: {selectedFace.emotion}</li>
                    </ul>
                </div>
            )}
        </div>
    );
};

FaceRecognition.propTypes = {
    imageUrl: PropTypes.string.isRequired,
    faceData: PropTypes.arrayOf(
        PropTypes.shape({
            boundingBox: PropTypes.shape({
                top: PropTypes.number.isRequired,
                left: PropTypes.number.isRequired,
                width: PropTypes.number.isRequired,
                height: PropTypes.number.isRequired,
            }).isRequired,
            age: PropTypes.number,
            gender: PropTypes.string,
            emotion: PropTypes.string,
        })
    ),
};

export default FaceRecognition;

