import { useState } from 'react';
import PropTypes from 'prop-types';
import './styles/SearchRefinement.css';

const SearchRefinement = ({ onApplyFilters }) => {
    const [dateRange, setDateRange] = useState({ start: '', end: '' });
    const [selectedTags, setSelectedTags] = useState([]);
    const [availableTags] = useState(['Nature', 'Portraits', 'Events', 'Urban', 'Abstract']);

    const handleTagToggle = (tag) => {
        setSelectedTags((prevTags) =>
            prevTags.includes(tag)
                ? prevTags.filter((t) => t !== tag)
                : [...prevTags, tag]
        );
    };

    const handleApplyFilters = () => {
        onApplyFilters({
            dateRange,
            tags: selectedTags,
        });
    };

    return (
        <div className="search-refinement">
            <h3>Refine Your Search</h3>

            <div className="filter-section">
                <h4>Date Range</h4>
                <label>
                    Start Date:
                    <input
                        type="date"
                        value={dateRange.start}
                        onChange={(e) => setDateRange({ ...dateRange, start: e.target.value })}
                    />
                </label>
                <label>
                    End Date:
                    <input
                        type="date"
                        value={dateRange.end}
                        onChange={(e) => setDateRange({ ...dateRange, end: e.target.value })}
                    />
                </label>
            </div>

            <div className="filter-section">
                <h4>Tags</h4>
                <div className="tag-options">
                    {availableTags.map((tag) => (
                        <label key={tag}>
                            <input
                                type="checkbox"
                                checked={selectedTags.includes(tag)}
                                onChange={() => handleTagToggle(tag)}
                            />
                            {tag}
                        </label>
                    ))}
                </div>
            </div>

            <button className="apply-button" onClick={handleApplyFilters}>
                Apply Filters
            </button>
        </div>
    );
};

SearchRefinement.propTypes = {
    onApplyFilters: PropTypes.func.isRequired,
};

export default SearchRefinement;

