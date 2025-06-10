import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import React from 'react';
import ImageUpload from '@frontend/components/ImageUpload.jsx';

// Helper to get today's date in YYYY-MM-DD format
function today() {
  return new Date().toISOString().split('T')[0];
}

describe('ImageUpload', () => {
  it("initializes timestamp with today's date", () => {
    render(<ImageUpload />);
    const dateInput = screen.getByPlaceholderText('Enter timestamp (yyyy-MM-dd)');
    expect(dateInput.value).toBe(today());
  });
});
