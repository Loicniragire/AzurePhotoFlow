import { useState } from 'react';
import searchService from '../services/searchService';
import '../styles/ImageSearch.css';

const ImageSearch = () => {
    const [searchQuery, setSearchQuery] = useState('');
    const [searchResults, setSearchResults] = useState([]);
    const [isLoading, setIsLoading] = useState(false);
    const [error, setError] = useState('');

    const handleSearch = async () => {
        if (!searchQuery.trim()) {
            alert('Please enter a search query.');
            return;
        }

        setIsLoading(true);
        setError('');

        try {
            const response = await searchService.searchSemantic(searchQuery);
            setSearchResults(response.results || []);
        } catch (err) {
            setError(err.message || 'Failed to fetch search results. Please try again later.');
            console.error('Search error:', err);
        } finally {
            setIsLoading(false);
        }
    };

    return (
        <div className="image-search">
            <h2>Search for Images</h2>
            <div className="search-bar">
                <input
                    type="text"
                    placeholder="Enter search query..."
                    value={searchQuery}
                    onChange={(e) => setSearchQuery(e.target.value)}
                />
                <button onClick={handleSearch}>Search</button>
            </div>

            {isLoading && <p>Loading...</p>}

            {error && <p className="error-message">{error}</p>}

            <div className="search-results">
                {searchResults.length > 0 ? (
                    <ul>
                        {searchResults.map((result, index) => (
                            <li key={result.objectKey || index} className="search-result-item">
                                <img src={result.imageUrl} alt={result.altText || 'Image'} />
                                <div className="result-metadata">
                                    <p><strong>File:</strong> {result.metadata?.fileName || 'Unknown'}</p>
                                    {result.metadata?.projectName && <p><strong>Project:</strong> {result.metadata.projectName}</p>}
                                    {result.similarityScore && <p><strong>Score:</strong> {(result.similarityScore * 100).toFixed(1)}%</p>}
                                </div>
                            </li>
                        ))}
                    </ul>
                ) : (
                    !isLoading && <p>No results found.</p>
                )}
            </div>
        </div>
    );
};

export default ImageSearch;

