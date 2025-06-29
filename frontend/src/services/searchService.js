import apiClient from './apiClient';

/**
 * Transform backend search results to frontend format
 */
const transformSearchResults = (results) => {
    return results.map(result => ({
        // Use GUID-based URL if available, fallback to object key for backward compatibility
        imageUrl: result.id ? `/api/image/by-id/${result.id}` : 
                  result.objectKey ? `/api/image/${encodeURIComponent(result.objectKey)}` : null,
        altText: result.fileName || result.metadata?.fileName || 'Image',
        metadata: {
            fileName: result.fileName || result.metadata?.fileName,
            uploadDate: result.uploadDate || result.metadata?.uploadDate,
            fileSize: result.fileSize || result.metadata?.fileSize,
            projectName: result.projectName || result.metadata?.projectName,
            year: result.year || result.metadata?.year,
            directory: result.directoryName || result.metadata?.directory
        },
        id: result.id, // GUID identifier
        objectKey: result.objectKey, // For backward compatibility
        similarityScore: result.similarityScore || result.score
    }));
};

/**
 * Semantic Search Service
 * Searches for images using natural language queries
 */
export const searchSemantic = async (query, options = {}) => {
    try {
        // Convert similarity ranges to min/max threshold
        let minThreshold = 0;
        let maxThreshold = 1;
        
        if (options.similarityRanges) {
            const ranges = options.similarityRanges;
            const activeRanges = [];
            
            if (ranges.strong) activeRanges.push({ min: 0.25, max: 0.40 });
            if (ranges.moderate) activeRanges.push({ min: 0.18, max: 0.25 });
            if (ranges.weak) activeRanges.push({ min: 0.10, max: 0.18 });
            if (ranges.poor) activeRanges.push({ min: 0, max: 0.10 });
            
            if (activeRanges.length > 0) {
                minThreshold = Math.min(...activeRanges.map(r => r.min));
                maxThreshold = Math.max(...activeRanges.map(r => r.max));
            }
        }
        
        const params = {
            query,
            limit: options.limit || 20,
            threshold: options.threshold || minThreshold,
            maxThreshold: maxThreshold < 1 ? maxThreshold : undefined,
            ...(options.projectName && { projectName: options.projectName }),
            ...(options.year && { year: options.year })
        };

        const response = await apiClient.get('/api/search/semantic', { params });
        
        return {
            results: transformSearchResults(response.data.results || []),
            totalResults: response.data.totalResults || 0,
            totalImagesSearched: response.data.totalImagesSearched || 0,
            collectionName: response.data.collectionName || 'unknown',
            processingTimeMs: response.data.processingTimeMs || 0,
            query: response.data.query
        };
    } catch (error) {
        console.error('Semantic search failed:', error);
        throw new Error(error.message || 'Failed to perform semantic search');
    }
};

/**
 * Similarity Search Service
 * Finds visually similar images to a reference image
 */
export const searchSimilarity = async (objectKey, options = {}) => {
    try {
        // Convert similarity ranges to min/max threshold
        let minThreshold = 0;
        let maxThreshold = 1;
        
        if (options.similarityRanges) {
            const ranges = options.similarityRanges;
            const activeRanges = [];
            
            if (ranges.strong) activeRanges.push({ min: 0.25, max: 0.40 });
            if (ranges.moderate) activeRanges.push({ min: 0.18, max: 0.25 });
            if (ranges.weak) activeRanges.push({ min: 0.10, max: 0.18 });
            if (ranges.poor) activeRanges.push({ min: 0, max: 0.10 });
            
            if (activeRanges.length > 0) {
                minThreshold = Math.min(...activeRanges.map(r => r.min));
                maxThreshold = Math.max(...activeRanges.map(r => r.max));
            }
        }
        
        const params = {
            objectKey,
            limit: options.limit || 20,
            threshold: options.threshold || minThreshold,
            maxThreshold: maxThreshold < 1 ? maxThreshold : undefined,
            ...(options.projectName && { projectName: options.projectName }),
            ...(options.year && { year: options.year })
        };

        const response = await apiClient.get('/api/search/similarity', { params });
        
        return {
            results: transformSearchResults(response.data.results || []),
            totalResults: response.data.totalResults || 0,
            processingTimeMs: response.data.processingTimeMs || 0,
            referenceObjectKey: response.data.referenceObjectKey
        };
    } catch (error) {
        console.error('Similarity search failed:', error);
        throw new Error(error.message || 'Failed to perform similarity search');
    }
};

/**
 * Advanced Similarity Search Service
 * GET endpoint with query parameters for similarity search requests
 */
export const searchSimilarityAdvanced = async (searchRequest) => {
    try {
        // Convert similarity ranges to min/max threshold
        let minThreshold = 0;
        let maxThreshold = 1;
        
        if (searchRequest.similarityRanges) {
            const ranges = searchRequest.similarityRanges;
            const activeRanges = [];
            
            if (ranges.strong) activeRanges.push({ min: 0.25, max: 0.40 });
            if (ranges.moderate) activeRanges.push({ min: 0.18, max: 0.25 });
            if (ranges.weak) activeRanges.push({ min: 0.10, max: 0.18 });
            if (ranges.poor) activeRanges.push({ min: 0, max: 0.10 });
            
            if (activeRanges.length > 0) {
                minThreshold = Math.min(...activeRanges.map(r => r.min));
                maxThreshold = Math.max(...activeRanges.map(r => r.max));
            }
        }
        
        const params = {
            objectKey: searchRequest.objectKey,
            limit: searchRequest.limit || 20,
            threshold: searchRequest.threshold || minThreshold,
            maxThreshold: maxThreshold < 1 ? maxThreshold : undefined,
            ...(searchRequest.projectName && { projectName: searchRequest.projectName }),
            ...(searchRequest.year && { year: searchRequest.year })
        };

        const response = await apiClient.get('/api/search/similarity', { params });
        
        return {
            results: transformSearchResults(response.data.results || []),
            totalResults: response.data.totalResults || 0,
            processingTimeMs: response.data.processingTimeMs || 0,
            referenceObjectKey: response.data.referenceObjectKey
        };
    } catch (error) {
        console.error('Advanced similarity search failed:', error);
        throw new Error(error.message || 'Failed to perform advanced similarity search');
    }
};

/**
 * Complex Multi-Criteria Search Service
 * Combines semantic and similarity search with advanced filtering
 */
export const searchComplex = async (searchRequest) => {
    try {
        const response = await apiClient.post('/api/search/query', searchRequest);
        
        return {
            results: transformSearchResults(response.data.results || []),
            totalResults: response.data.totalResults || 0,
            processingTimeMs: response.data.processingTimeMs || 0,
            searchResultBreakdown: response.data.searchResultBreakdown || null,
            appliedFilters: response.data.appliedFilters || null
        };
    } catch (error) {
        console.error('Complex search failed:', error);
        throw new Error(error.message || 'Failed to perform complex search');
    }
};

/**
 * Debug Search Service - Get ALL similarity scores
 * Returns all images in collection with their similarity scores for debugging
 */
export const searchDebugAllScores = async (query, options = {}) => {
    try {
        const params = {
            query,
            ...(options.projectName && { projectName: options.projectName }),
            ...(options.year && { year: options.year })
        };

        const response = await apiClient.get('/api/search/debug/all-scores', { params });
        
        return {
            results: transformSearchResults(response.data.results || []),
            totalResults: response.data.totalResults || 0,
            totalImagesSearched: response.data.totalImagesSearched || 0,
            collectionName: response.data.collectionName || 'unknown',
            processingTimeMs: response.data.processingTimeMs || 0,
            query: response.data.query
        };
    } catch (error) {
        console.error('Debug all-scores search failed:', error);
        throw new Error(error.message || 'Failed to perform debug all-scores search');
    }
};

/**
 * Get image URL for display
 */
export const getImageUrl = (objectKeyOrId) => {
    if (!objectKeyOrId) return null;
    
    // Check if it's a GUID format (standard GUID with hyphens)
    const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
    
    if (guidPattern.test(objectKeyOrId)) {
        // Use GUID-based endpoint
        return `/api/image/by-id/${objectKeyOrId}`;
    } else {
        // Use object key endpoint with URL encoding
        return `/api/image/${encodeURIComponent(objectKeyOrId)}`;
    }
};

/**
 * Search service object with all methods
 */
const searchService = {
    searchSemantic,
    searchSimilarity,
    searchSimilarityAdvanced,
    searchComplex,
    searchDebugAllScores,
    getImageUrl
};

export default searchService;
