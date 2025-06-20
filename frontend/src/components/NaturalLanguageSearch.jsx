import { useState } from 'react';
import searchService from '../services/searchService';
import '../styles/NaturalLanguageSearch.css';

const NaturalLanguageSearch = () => {
    const [query, setQuery] = useState('');
    const [results, setResults] = useState([]);
    const [isLoading, setIsLoading] = useState(false);
    const [error, setError] = useState('');
    const [searchMeta, setSearchMeta] = useState(null);

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
            setSearchMeta({
                totalResults: response.totalResults || 0,
                totalImagesSearched: response.totalImagesSearched || 0,
                processingTime: response.processingTimeMs || 0,
                query: response.query || query
            });

            // Only set error if there are truly no images in the collection
            if (response.totalImagesSearched === 0) {
                setError('No images found in collection. Try uploading some images first.');
            }
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

            {/* Search metadata */}
            {searchMeta && (
                <div className="search-meta">
                    <div className="search-stats">
                        <p>
                            Found {searchMeta.totalResults} result{searchMeta.totalResults !== 1 ? 's' : ''} 
                            for &quot;{searchMeta.query}&quot; in {searchMeta.processingTime}ms
                        </p>
                        <p className="search-scope">
                            Searched through {searchMeta.totalImagesSearched.toLocaleString()} image{searchMeta.totalImagesSearched !== 1 ? 's' : ''}
                            {searchMeta.totalImagesSearched === 0 && " (No images found in collection - try uploading some images first)"}
                        </p>
                    </div>
                </div>
            )}

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
                    !isLoading && searchMeta && (
                        <div className="no-results">
                            {searchMeta.totalImagesSearched > 0 ? (
                                <>
                                    <p>🔍 No images found matching your search.</p>
                                    <p>Try different keywords or check your search terms.</p>
                                </>
                            ) : (
                                <>
                                    <p>📁 No images available to search.</p>
                                    <p>Upload some images first to enable searching.</p>
                                </>
                            )}
                        </div>
                    )
                )}
            </div>
        </div>
    );
};

export default NaturalLanguageSearch;

