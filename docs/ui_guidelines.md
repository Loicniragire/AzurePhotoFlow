# UI Guidelines & Design Standards

This document outlines the user interface guidelines and design standards for AzurePhotoFlow frontend development.

## Design System

### Technology Stack
- **Framework**: React 18 with JSX
- **Build Tool**: Vite for development and bundling
- **UI Library**: Material-UI (MUI) v6
- **Styling**: CSS-in-JS with MUI's styled components
- **Icons**: Material Design Icons
- **Routing**: React Router DOM v7

### Component Guidelines

#### Key Components
- **LoginPage**: Google OAuth integration
- **Dashboard**: Project overview and statistics
- **ImageUpload**: Drag-and-drop ZIP file upload
- **ImageSearch**: Search interface with filters
- **NaturalLanguageSearch**: AI-powered semantic search

### Development Standards

#### File Structure
```
src/
   components/          # Reusable UI components
   pages/              # Page-level components
   services/           # API and business logic
   styles/             # Global styles and themes
   utils/              # Helper functions
```

#### Code Quality
- **ESLint**: Enforce code style and catch errors
- **Prettier**: Consistent code formatting
- **Testing**: Vitest + React Testing Library for unit tests

## Getting Started

For detailed development setup, see the [Setup Guide](setup.md).

## Resources

- [Material-UI Documentation](https://mui.com/)
- [React Documentation](https://react.dev/)
- [Vite Documentation](https://vitejs.dev/)