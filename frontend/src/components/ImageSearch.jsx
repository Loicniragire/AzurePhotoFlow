import { useState, useEffect } from 'react';
import searchService from '../services/searchService';
import apiClient from '../services/apiClient';
import '../styles/ImageSearch.css';

const ImageSearch = () => {
    const [searchQuery, setSearchQuery] = useState('');
    const [searchResults, setSearchResults] = useState([]);
    const [isLoading, setIsLoading] = useState(false);
    const [error, setError] = useState('');
    const [searchMeta, setSearchMeta] = useState(null);
    const [hasSearched, setHasSearched] = useState(false);
    
    // Search parameters
    const [filters, setFilters] = useState({
        year: '',
        projectName: '',
        limit: 20,
        threshold: 0.5
    });
    
    // Available options for filters
    const [availableProjects, setAvailableProjects] = useState([]);
    const [availableYears, setAvailableYears] = useState([]);
    const [filtersLoading, setFiltersLoading] = useState(false);

    // Load available projects and years for filtering
    useEffect(() => {
        const loadFilterOptions = async () => {
            setFiltersLoading(true);
            try {
                const response = await apiClient.get('/api/image/projects');
                const projects = response.data || [];
                
                // Extract unique project names and years
                const uniqueProjects = [...new Set(projects.map(p => p.Name).filter(Boolean))];
                const uniqueYears = [...new Set(projects.map(p => new Date(p.Datestamp).getFullYear()).filter(Boolean))];
                
                setAvailableProjects(uniqueProjects);
                setAvailableYears(uniqueYears.sort((a, b) => b - a)); // Sort years descending
            } catch (err) {
                console.error('Failed to load filter options:', err);
                // Don't show error for this, it's optional
            } finally {
                setFiltersLoading(false);
            }
        };

        loadFilterOptions();
    }, []);

    const handleSearch = async () => {
        if (!searchQuery.trim()) {
            setError('Please enter a search query.');
            return;
        }

        setIsLoading(true);
        setError('');
        setHasSearched(true);

        try {
            // Prepare search options with filters
            const searchOptions = {};
            if (filters.projectName) searchOptions.projectName = filters.projectName;
            if (filters.year) searchOptions.year = filters.year;
            if (filters.limit && filters.limit !== 20) searchOptions.limit = filters.limit;
            if (filters.threshold && filters.threshold !== 0.5) searchOptions.threshold = filters.threshold;

            const response = await searchService.searchSemantic(searchQuery, searchOptions);
            setSearchResults(response.results || []);
            setSearchMeta({
                totalResults: response.totalResults || 0,
                processingTime: response.processingTimeMs || 0,
                query: response.query || searchQuery,
                appliedFilters: searchOptions
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

            {/* Search Filters */}
            <div className="search-filters">
                <div className="filter-row">
                    <div className="filter-group">
                        <label htmlFor="project-filter">Project:</label>
                        <select
                            id="project-filter"
                            value={filters.projectName}
                            onChange={(e) => setFilters(prev => ({ ...prev, projectName: e.target.value }))}
                            disabled={filtersLoading}
                        >
                            <option value="">All Projects</option>
                            {availableProjects.map(project => (
                                <option key={project} value={project}>{project}</option>
                            ))}
                        </select>
                    </div>

                    <div className="filter-group">
                        <label htmlFor="year-filter">Year:</label>
                        <select
                            id="year-filter"
                            value={filters.year}
                            onChange={(e) => setFilters(prev => ({ ...prev, year: e.target.value }))}
                            disabled={filtersLoading}
                        >
                            <option value="">All Years</option>
                            {availableYears.map(year => (
                                <option key={year} value={year}>{year}</option>
                            ))}
                        </select>
                    </div>

                    <div className="filter-group">
                        <label htmlFor="limit-filter">Results:</label>
                        <select
                            id="limit-filter"
                            value={filters.limit}
                            onChange={(e) => setFilters(prev => ({ ...prev, limit: parseInt(e.target.value) }))}
                        >
                            <option value={10}>10</option>
                            <option value={20}>20</option>
                            <option value={50}>50</option>
                            <option value={100}>100</option>
                        </select>
                    </div>

                    <div className="filter-group">
                        <label htmlFor="threshold-filter">Similarity:</label>
                        <select
                            id="threshold-filter"
                            value={filters.threshold}
                            onChange={(e) => setFilters(prev => ({ ...prev, threshold: parseFloat(e.target.value) }))}
                        >
                            <option value={0.3}>Low (30%)</option>
                            <option value={0.5}>Medium (50%)</option>
                            <option value={0.7}>High (70%)</option>
                            <option value={0.9}>Very High (90%)</option>
                        </select>
                    </div>
                </div>
            </div>
            
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
                    <div className="search-stats">
                        <p>
                            Found {searchMeta.totalResults} result{searchMeta.totalResults !== 1 ? 's' : ''} 
                            for "{searchMeta.query}" in {searchMeta.processingTime}ms
                        </p>
                        {searchMeta.appliedFilters && Object.keys(searchMeta.appliedFilters).length > 0 && (
                            <div className="applied-filters">
                                <span>Filters: </span>
                                {Object.entries(searchMeta.appliedFilters).map(([key, value]) => (
                                    <span key={key} className="filter-tag">
                                        {key === 'projectName' ? 'Project' : 
                                         key === 'threshold' ? 'Similarity' : 
                                         key.charAt(0).toUpperCase() + key.slice(1)}: {value}
                                    </span>
                                ))}
                            </div>
                        )}
                    </div>
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

