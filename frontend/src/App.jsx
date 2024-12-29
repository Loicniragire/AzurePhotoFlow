import { BrowserRouter as Router, Routes, Route } from 'react-router-dom';
import ImageUpload from './components/ImageUpload';
import ImageSearch from './components/ImageSearch';
import NaturalLanguageSearch from './components/NaturalLanguageSearch';
import Dashboard from './components/Dashboard';
import FaceRecognition from './components/FaceRecognition';
import './App.css';

const App = () => {
    return (
        <Router>
            <div className="app-container">
                <header className="app-header">
                    <h1>Photo Flow</h1>
                    <nav>
                        <ul>
                            <li><a href="/upload">Upload</a></li>
                            <li><a href="/search">Search</a></li>
                            <li><a href="/naturallanguage">Natural Language Search</a></li>
                            <li><a href="/dashboard">Dashboard</a></li>
                            <li><a href="/facerecognition">Face Recognition</a></li>
                        </ul>
                    </nav>
                </header>

                <main className="app-main">
                    <Routes>
                        <Route path="/upload" element={<ImageUpload />} />
                        <Route path="/search" element={<ImageSearch />} />
                        <Route path="/naturallanguage" element={<NaturalLanguageSearch />} />
                        <Route path="/dashboard" element={<Dashboard />} />
                        <Route path="/facerecognition" element={<FaceRecognition />} />
                        <Route path="*" element={<h2>Welcome to AzurePhotoFlow!</h2>} />
                    </Routes>
                </main>
            </div>
        </Router>
    );
};

export default App;

