import { describe, it, expect } from 'vitest';
import { extractFilterOptions } from '@frontend/components/ImageSearch.jsx';

const sampleProjects = [
  { projectName: 'project1', timestamp: '2023-05-01T00:00:00Z' },
  { projectName: 'project2', timestamp: '2024-06-15T00:00:00Z' },
];

describe('ImageSearch filter extraction', () => {
  it('returns unique project names and years', () => {
    const { uniqueProjects, uniqueYears } = extractFilterOptions(sampleProjects);
    expect(uniqueProjects).toEqual(['project1', 'project2']);
    expect(uniqueYears).toEqual([2023, 2024]);
  });
});
