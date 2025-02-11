import React, { useMemo } from 'react';
import { useQuery } from 'react-query';
import { getProjects } from '../services/imageUploadApi';
import '../styles/Dashboard.css';

const fetchProjects = async () => {
  const projectsData = await getProjects();
  return projectsData || [];
};

const Dashboard = () => {
  // Configure React Query with a key, fetch function, and caching parameters.
  const { data: projects, isLoading, error } = useQuery('projects', fetchProjects, {
    staleTime: 60000,    // Cached data is considered fresh for 60 seconds
    cacheTime: 300000,   // Unused cached data is garbage collected after 5 minutes
    refetchOnWindowFocus: false, // Prevents refetching when window regains focus
  });

  // Memoized summary calculation
  const summary = useMemo(() => {
    if (!projects || !projects.length) return null;
    return projects.reduce(
      (acc, project) => {
        project.Directories.forEach((directory) => {
          acc.totalRawFiles += directory.RawFilesCount || 0;
          acc.totalProcessedFiles += directory.ProcessedFilesCount || 0;
        });
        acc.totalProjects += 1;
        return acc;
      },
      { totalRawFiles: 0, totalProcessedFiles: 0, totalProjects: 0 }
    );
  }, [projects]);

  if (isLoading) return <p>Loading dashboard data...</p>;
  if (error) return <p className="error-message">Failed to load dashboard data.</p>;
  if (!summary) return <p>No project data available.</p>;

  return (
    <div className="dashboard">
      <h2>Dashboard</h2>
      <div className="stats-container">
        <h3>Project Summary</h3>
        <ul>
          <li>
            <strong>Total Projects:</strong> {summary.totalProjects}
          </li>
          <li>
            <strong>Total Raw Files:</strong> {summary.totalRawFiles}
          </li>
          <li>
            <strong>Total Processed Files:</strong> {summary.totalProcessedFiles}
          </li>
        </ul>
      </div>
    </div>
  );
};

export default Dashboard;

