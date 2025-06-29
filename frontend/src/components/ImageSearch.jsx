import { useState, useEffect } from 'react';
import searchService from '../services/searchService';
import apiClient from '../services/apiClient';
import '../styles/ImageSearch.css';

export const extractFilterOptions = (projects) => {
    const uniqueProjects = [...new Set(projects.map(p => p.projectName).filter(Boolean))];
    const uniqueYears = [...new Set(projects.map(p => new Date(p.timestamp).getFullYear()).filter(Boolean))];
    return { uniqueProjects, uniqueYears };
};

const ImageSearchNew = () => {
    const [searchQuery, setSearchQuery] = useState('');
    const [searchResults, setSearchResults] = useState([]);
    const [isLoading, setIsLoading] = useState(false);
    const [error, setError] = useState('');
    const [searchMeta, setSearchMeta] = useState(null);
    const [hasSearched, setHasSearched] = useState(false);
    const [debugMode, setDebugMode] = useState(false);
    
    // Search parameters
    const [filters, setFilters] = useState({
        year: '',
        projectName: '',
        limit: 20,
        similarityRanges: {
            strong: true,    // 0.25-0.40
            moderate: true,  // 0.18-0.25
            weak: true,      // 0.10-0.18
            poor: false      // Below 0.10
        }
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

                const { uniqueProjects, uniqueYears } = extractFilterOptions(projects);

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
            
            // Pass similarity ranges to the search service for processing
            searchOptions.similarityRanges = filters.similarityRanges;

            // Use debug endpoint if debug mode is enabled
            const response = debugMode 
                ? await searchService.searchDebugAllScores(searchQuery, searchOptions)
                : await searchService.searchSemantic(searchQuery, searchOptions);
            setSearchResults(response.results || []);
            setSearchMeta({
                totalResults: response.totalResults || 0,
                totalImagesSearched: response.totalImagesSearched || 0,
                collectionName: response.collectionName || 'unknown',
                processingTime: response.processingTimeMs || 0,
                query: response.query || searchQuery,
                appliedFilters: searchOptions,
                debugMode: debugMode
            });

            // Only set error if there are truly no images in the collection
            // Let the UI handle "no results" display without setting error state
            if (response.totalImagesSearched === 0) {
                setError('No images found in collection. Try uploading some images first.');
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
                Search your images using natural language. Try: &quot;dogs playing&quot;, &quot;sunset photos&quot;, &quot;people at beach&quot;
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
                        <label>Similarity Ranges:</label>
                        <div className="similarity-checkboxes">
                            <div className="checkbox-item">
                                <input
                                    type="checkbox"
                                    id="strong-matches"
                                    checked={filters.similarityRanges.strong}
                                    onChange={(e) => setFilters(prev => ({
                                        ...prev,
                                        similarityRanges: {
                                            ...prev.similarityRanges,
                                            strong: e.target.checked
                                        }
                                    }))}
                                />
                                <label htmlFor="strong-matches">Strong matches (25-40%)</label>
                            </div>
                            <div className="checkbox-item">
                                <input
                                    type="checkbox"
                                    id="moderate-matches"
                                    checked={filters.similarityRanges.moderate}
                                    onChange={(e) => setFilters(prev => ({
                                        ...prev,
                                        similarityRanges: {
                                            ...prev.similarityRanges,
                                            moderate: e.target.checked
                                        }
                                    }))}
                                />
                                <label htmlFor="moderate-matches">Moderate matches (18-25%)</label>
                            </div>
                            <div className="checkbox-item">
                                <input
                                    type="checkbox"
                                    id="weak-matches"
                                    checked={filters.similarityRanges.weak}
                                    onChange={(e) => setFilters(prev => ({
                                        ...prev,
                                        similarityRanges: {
                                            ...prev.similarityRanges,
                                            weak: e.target.checked
                                        }
                                    }))}
                                />
                                <label htmlFor="weak-matches">Weak matches (10-18%)</label>
                            </div>
                            <div className="checkbox-item">
                                <input
                                    type="checkbox"
                                    id="poor-matches"
                                    checked={filters.similarityRanges.poor}
                                    onChange={(e) => setFilters(prev => ({
                                        ...prev,
                                        similarityRanges: {
                                            ...prev.similarityRanges,
                                            poor: e.target.checked
                                        }
                                    }))}
                                />
                                <label htmlFor="poor-matches">Poor matches (&lt;10%)</label>
                            </div>
                        </div>
                    </div>
                </div>

                {/* Debug Mode Toggle */}
                <div className="debug-mode-toggle">
                    <label className="debug-checkbox">
                        <input
                            type="checkbox"
                            checked={debugMode}
                            onChange={(e) => setDebugMode(e.target.checked)}
                        />
                        <span className="debug-label">
                            🔍 Debug Mode: Show ALL images with similarity scores (ignores threshold)
                        </span>
                    </label>
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
            {searchMeta && (
                <div className="search-meta">
                    <div className="search-stats">
                        <p>
                            {searchMeta.debugMode ? 'Debug: ' : ''}Found {searchMeta.totalResults} result{searchMeta.totalResults !== 1 ? 's' : ''} 
                            for &quot;{searchMeta.query}&quot; in {searchMeta.processingTime}ms
                            {searchMeta.debugMode && ' (showing ALL images)'}
                        </p>
                        <p className="search-scope">
                            Searched through {searchMeta.totalImagesSearched.toLocaleString()} image{searchMeta.totalImagesSearched !== 1 ? 's' : ''} in collection &quot;{searchMeta.collectionName}&quot;
                            {searchMeta.totalImagesSearched === 0 && " (No images found in collection - try uploading some images first)"}
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

            {/* Debug section - detailed list of all search results */}
            {searchResults.length > 0 && (
                <div className="debug-section">
                    <details className="debug-details">
                        <summary className="debug-summary">
                            🔍 Debug: Show All Search Results ({searchResults.length} items)
                        </summary>
                        <div className="debug-content">
                            <div className="debug-list">
                                {searchResults.map((result, index) => {
                                    const fileName = result.metadata?.fileName || result.objectKey?.split('/').pop() || `Unknown-${index + 1}`;
                                    const score = result.similarityScore || 0;
                                    return (
                                        <div key={result.objectKey || index} className="debug-item">
                                            <span className="debug-rank">#{index + 1}</span>
                                            <span className="debug-filename">{fileName}</span>
                                            <span className="debug-score">
                                                {(score * 100).toFixed(2)}%
                                            </span>
                                            <span className="debug-project">
                                                {result.metadata?.projectName || 'No Project'}
                                            </span>
                                        </div>
                                    );
                                })}
                            </div>
                            <div className="debug-stats">
                                <p><strong>Average Similarity:</strong> {searchResults.length > 0 ? 
                                    (searchResults.reduce((sum, r) => sum + (r.similarityScore || 0), 0) / searchResults.length * 100).toFixed(2) : 0}%
                                </p>
                                <p><strong>Highest Score:</strong> {searchResults.length > 0 ? 
                                    Math.max(...searchResults.map(r => r.similarityScore || 0)) * 100 : 0}%
                                </p>
                                <p><strong>Lowest Score:</strong> {searchResults.length > 0 ? 
                                    Math.min(...searchResults.map(r => r.similarityScore || 0)) * 100 : 0}%
                                </p>
                            </div>
                        </div>
                    </details>
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
                    hasSearched && !isLoading && (
                        <div className="no-results">
                            {searchMeta && searchMeta.totalImagesSearched > 0 ? (
                                <>
                                    <p>🔍 No images found matching your search.</p>
                                    <p>Try different keywords, adjust your similarity threshold, or check your filters.</p>
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

export default ImageSearchNew;

