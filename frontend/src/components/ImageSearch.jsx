import { useState } from 'react';
import axios from 'axios';
import './styles/ImageSearch.css';

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
            const response = await axios.get(`/api/search`, {
                params: { query: searchQuery },
            });

            setSearchResults(response.data.results || []);
        } catch (err) {
            setError('Failed to fetch search results. Please try again later.');
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
                            <li key={index} className="search-result-item">
                                <img src={result.imageUrl} alt={result.altText || 'Image'} />
                                <p>{result.metadata || 'No metadata available'}</p>
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

