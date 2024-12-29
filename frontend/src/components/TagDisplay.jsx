import PropTypes from 'prop-types';
import './styles/TagDisplay.css';

const TagDisplay = ({ tags = [], onTagClick }) => {
    if (!tags.length) {
        return <p className="no-tags">No tags available for this image.</p>;
    }

    return (
        <div className="tag-display">
            <h3>Tags</h3>
            <ul className="tag-list">
                {tags.map((tag, index) => (
                    <li
                        key={index}
                        className="tag-item"
                        onClick={() => onTagClick(tag)}
                    >
                        {tag}
                    </li>
                ))}
            </ul>
        </div>
    );
};

TagDisplay.propTypes = {
    tags: PropTypes.arrayOf(PropTypes.string),
    onTagClick: PropTypes.func.isRequired,
};

export default TagDisplay;

