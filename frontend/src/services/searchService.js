import apiClient from './apiClient';

/**
 * Transform backend search results to frontend format
 */
const transformSearchResults = (results) => {
    return results.map(result => ({
        imageUrl: result.objectKey ? `/api/images/${result.objectKey}` : null,
        altText: result.metadata?.fileName || 'Image',
        metadata: result.metadata ? {
            fileName: result.metadata.fileName,
            uploadDate: result.metadata.uploadDate,
            fileSize: result.metadata.fileSize,
            projectName: result.metadata.projectName,
            year: result.metadata.year,
            directory: result.metadata.directory
        } : null,
        objectKey: result.objectKey,
        similarityScore: result.similarityScore || result.score
    }));
};

/**
 * Semantic Search Service
 * Searches for images using natural language queries
 */
export const searchSemantic = async (query, options = {}) => {
    try {
        const params = {
            query,
            limit: options.limit || 20,
            threshold: options.threshold || 0.5,
            ...(options.projectName && { projectName: options.projectName }),
            ...(options.year && { year: options.year })
        };

        const response = await apiClient.get('/api/search/semantic', { params });
        
        return {
            results: transformSearchResults(response.data.results || []),
            totalResults: response.data.totalResults || 0,
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
        const params = {
            objectKey,
            limit: options.limit || 20,
            threshold: options.threshold || 0.5,
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
 * POST endpoint for more complex similarity search requests
 */
export const searchSimilarityAdvanced = async (searchRequest) => {
    try {
        const response = await apiClient.post('/api/search/similarity', searchRequest);
        
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
 * Get image URL for display
 */
export const getImageUrl = (objectKey) => {
    if (!objectKey) return null;
    return `/api/images/${objectKey}`;
};

/**
 * Search service object with all methods
 */
const searchService = {
    searchSemantic,
    searchSimilarity,
    searchSimilarityAdvanced,
    searchComplex,
    getImageUrl
};

export default searchService;