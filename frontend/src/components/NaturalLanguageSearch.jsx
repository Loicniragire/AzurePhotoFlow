import { useState } from 'react';
import searchService from '../services/searchService';
import '../styles/NaturalLanguageSearch.css';

const NaturalLanguageSearch = () => {
    const [query, setQuery] = useState('');
    const [results, setResults] = useState([]);
    const [isLoading, setIsLoading] = useState(false);
    const [error, setError] = useState('');

    const handleSearch = async () => {
        if (!query.trim()) {
            alert('Please enter a natural language query.');
            return;
        }

        setIsLoading(true);
        setError('');

        try {
            const response = await searchService.searchSemantic(query);
            setResults(response.results || []);
        } catch (err) {
            setError(err.message || 'Failed to process the query. Please try again later.');
            console.error('Search error:', err);
        } finally {
            setIsLoading(false);
        }
    };

    return (
        <div className="natural-language-search">
            <h2>Natural Language Search</h2>
            <textarea
                placeholder="Describe what you're looking for..."
                value={query}
                onChange={(e) => setQuery(e.target.value)}
            />
            <button onClick={handleSearch}>Search</button>

            {isLoading && <p>Loading...</p>}

            {error && <p className="error-message">{error}</p>}

            <div className="search-results">
                {results.length > 0 ? (
                    <ul>
                        {results.map((result, index) => (
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

export default NaturalLanguageSearch;

