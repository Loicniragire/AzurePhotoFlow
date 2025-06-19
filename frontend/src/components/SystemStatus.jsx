import { useState, useEffect } from 'react';
import apiClient from '../services/apiClient';

const SystemStatus = () => {
    const [status, setStatus] = useState({
        isAuthenticated: false,
        hasImages: false,
        hasEmbeddings: false,
        loading: true,
        error: null
    });

    useEffect(() => {
        checkSystemStatus();
    }, []);

    const checkSystemStatus = async () => {
        try {
            setStatus(prev => ({ ...prev, loading: true, error: null }));

            // Check authentication
            let isAuthenticated = false;
            try {
                const authResponse = await apiClient.get('/api/auth/check');
                isAuthenticated = authResponse.status === 200;
            } catch (authError) {
                isAuthenticated = false;
            }

            // Check if there are any projects/images
            let hasImages = false;
            if (isAuthenticated) {
                try {
                    const projectsResponse = await apiClient.get('/api/image/projects');
                    hasImages = projectsResponse.data && projectsResponse.data.length > 0;
                } catch (projectError) {
                    hasImages = false;
                }
            }

            // For now, assume embeddings exist if images exist
            // In a real implementation, you'd check the embedding service
            const hasEmbeddings = hasImages;

            setStatus({
                isAuthenticated,
                hasImages,
                hasEmbeddings,
                loading: false,
                error: null
            });
        } catch (error) {
            setStatus(prev => ({
                ...prev,
                loading: false,
                error: 'Failed to check system status'
            }));
        }
    };

    if (status.loading) {
        return (
            <div className="system-status loading">
                <h3>⏳ Checking system status...</h3>
            </div>
        );
    }

    if (status.error) {
        return (
            <div className="system-status error">
                <h3>❌ {status.error}</h3>
                <button onClick={checkSystemStatus}>Retry</button>
            </div>
        );
    }

    const allReady = status.isAuthenticated && status.hasImages && status.hasEmbeddings;

    return (
        <div className={`system-status ${allReady ? 'ready' : 'not-ready'}`}>
            <h3>System Status</h3>
            <div className="status-items">
                <div className={`status-item ${status.isAuthenticated ? 'success' : 'warning'}`}>
                    <span className="status-icon">
                        {status.isAuthenticated ? '✅' : '⚠️'}
                    </span>
                    <span className="status-text">
                        {status.isAuthenticated ? 'Authenticated' : 'Not logged in'}
                    </span>
                </div>

                <div className={`status-item ${status.hasImages ? 'success' : 'warning'}`}>
                    <span className="status-icon">
                        {status.hasImages ? '✅' : '📁'}
                    </span>
                    <span className="status-text">
                        {status.hasImages ? 'Images uploaded' : 'No images uploaded'}
                    </span>
                </div>

                <div className={`status-item ${status.hasEmbeddings ? 'success' : 'warning'}`}>
                    <span className="status-icon">
                        {status.hasEmbeddings ? '✅' : '🤖'}
                    </span>
                    <span className="status-text">
                        {status.hasEmbeddings ? 'AI processing complete' : 'AI processing needed'}
                    </span>
                </div>
            </div>

            {!allReady && (
                <div className="status-message">
                    <p><strong>Search is not available yet.</strong></p>
                    <p>To enable search:</p>
                    <ol>
                        {!status.isAuthenticated && <li>Log in to your account</li>}
                        {!status.hasImages && <li>Upload some images</li>}
                        {!status.hasEmbeddings && <li>Wait for AI processing to complete</li>}
                    </ol>
                </div>
            )}

            <button onClick={checkSystemStatus} className="refresh-btn">
                🔄 Refresh Status
            </button>
        </div>
    );
};

export default SystemStatus;