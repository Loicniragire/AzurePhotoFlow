import { useState, useEffect } from 'react';
import searchService from '../services/searchService';
import SystemStatus from './SystemStatus';
import '../styles/ImageSearch.css';
import '../styles/SystemStatus.css';

const ImageSearch = () => {
    const [searchQuery, setSearchQuery] = useState('');
    const [searchResults, setSearchResults] = useState([]);
    const [isLoading, setIsLoading] = useState(false);
    const [error, setError] = useState('');
    const [searchMeta, setSearchMeta] = useState(null);
    const [hasSearched, setHasSearched] = useState(false);

    const handleSearch = async () => {
        if (!searchQuery.trim()) {
            setError('Please enter a search query.');
            return;
        }

        setIsLoading(true);
        setError('');
        setHasSearched(true);

        try {
            const response = await searchService.searchSemantic(searchQuery);
            setSearchResults(response.results || []);
            setSearchMeta({
                totalResults: response.totalResults || 0,
                processingTime: response.processingTimeMs || 0,
                query: response.query || searchQuery
            });

            // If no results, provide helpful feedback
            if (!response.results || response.results.length === 0) {
                setError('No images found for your search. Try uploading some images first or use different search terms.');
            }
        } catch (err) {
            console.error('Search error:', err);
            
            // Enhanced error handling
            if (err.message.includes('401')) {
                setError('Please log in to search for images.');
            } else if (err.message.includes('Network')) {
                setError('Connection error. Please check your internet connection and try again.');
            } else {
                setError(err.message || 'Search failed. This might be because no images have been uploaded yet.');
            }
        } finally {
            setIsLoading(false);
        }
    };

    const handleKeyPress = (e) => {
        if (e.key === 'Enter') {
            handleSearch();
        }
    };

    const clearSearch = () => {
        setSearchQuery('');
        setSearchResults([]);
        setError('');
        setSearchMeta(null);
        setHasSearched(false);
    };

    return (
        <div className="image-search">
            <h2>AI-Powered Image Search</h2>
            <p className="search-description">
                Search your images using natural language. Try: "dogs playing", "sunset photos", "people at beach"
            </p>

            {/* System status check */}
            <SystemStatus />
            
            <div className="search-bar">
                <input
                    type="text"
                    placeholder="Describe what you're looking for..."
                    value={searchQuery}
                    onChange={(e) => setSearchQuery(e.target.value)}
                    onKeyPress={handleKeyPress}
                    disabled={isLoading}
                />
                <button onClick={handleSearch} disabled={isLoading || !searchQuery.trim()}>
                    {isLoading ? 'Searching...' : 'Search'}
                </button>
                {searchQuery && (
                    <button onClick={clearSearch} className="clear-button" type="button">
                        Clear
                    </button>
                )}
            </div>

            {/* Search suggestions for first-time users */}
            {!hasSearched && (
                <div className="search-suggestions">
                    <p>Try these example searches:</p>
                    <div className="suggestion-buttons">
                        <button onClick={() => setSearchQuery('dogs')} className="suggestion-btn">dogs</button>
                        <button onClick={() => setSearchQuery('sunset')} className="suggestion-btn">sunset</button>
                        <button onClick={() => setSearchQuery('people')} className="suggestion-btn">people</button>
                        <button onClick={() => setSearchQuery('flowers')} className="suggestion-btn">flowers</button>
                    </div>
                </div>
            )}

            {/* Loading state */}
            {isLoading && (
                <div className="loading-state">
                    <div className="loading-spinner"></div>
                    <p>Analyzing your query and searching through images...</p>
                </div>
            )}

            {/* Error state */}
            {error && (
                <div className="error-state">
                    <div className="error-message">
                        <strong>❌ {error}</strong>
                    </div>
                    {error.includes('no images') && (
                        <div className="help-text">
                            <p>To get started:</p>
                            <ol>
                                <li>Upload some images using the Upload page</li>
                                <li>Wait for AI processing to complete</li>
                                <li>Then try searching again</li>
                            </ol>
                        </div>
                    )}
                </div>
            )}

            {/* Search metadata */}
            {searchMeta && !error && (
                <div className="search-meta">
                    <p>
                        Found {searchMeta.totalResults} result{searchMeta.totalResults !== 1 ? 's' : ''} 
                        for "{searchMeta.query}" in {searchMeta.processingTime}ms
                    </p>
                </div>
            )}

            {/* Search results */}
            <div className="search-results">
                {searchResults.length > 0 ? (
                    <div className="results-grid">
                        {searchResults.map((result, index) => (
                            <div key={result.objectKey || index} className="search-result-item">
                                <div className="image-container">
                                    <img 
                                        src={result.imageUrl} 
                                        alt={result.altText || 'Search result'} 
                                        onError={(e) => {
                                            e.target.src = '/placeholder-image.png';
                                            e.target.alt = 'Image not available';
                                        }}
                                    />
                                    {result.similarityScore && (
                                        <div className="similarity-badge">
                                            {(result.similarityScore * 100).toFixed(0)}% match
                                        </div>
                                    )}
                                </div>
                                <div className="result-metadata">
                                    <h4>{result.metadata?.fileName || 'Unknown File'}</h4>
                                    {result.metadata?.projectName && (
                                        <p className="project-name">📁 {result.metadata.projectName}</p>
                                    )}
                                    {result.metadata?.uploadDate && (
                                        <p className="upload-date">
                                            📅 {new Date(result.metadata.uploadDate).toLocaleDateString()}
                                        </p>
                                    )}
                                </div>
                            </div>
                        ))}
                    </div>
                ) : (
                    hasSearched && !isLoading && !error && (
                        <div className="no-results">
                            <p>🔍 No images found matching your search.</p>
                            <p>Try different keywords or upload more images.</p>
                        </div>
                    )
                )}
            </div>
        </div>
    );
};

export default ImageSearch;

