import { describe, it, expect } from 'vitest';
import { formatDate } from '@frontend/utils/formatDate.js';

describe('formatDate', () => {
  it('returns ISO date string', () => {
    expect(formatDate('2024-05-20')).toBe('2024-05-20');
  });

  it('returns null for invalid date', () => {
    expect(formatDate('invalid')).toBeNull();
  });
});
