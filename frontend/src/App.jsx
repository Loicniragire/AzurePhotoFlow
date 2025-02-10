import React, { useState, useEffect } from "react";
import {
  BrowserRouter as Router,
  Routes,
  Route,
  NavLink,
} from "react-router-dom";
import Home from "./components/Home";
import ImageUpload from "./components/ImageUpload";
import ImageSearch from "./components/ImageSearch";
import NaturalLanguageSearch from "./components/NaturalLanguageSearch";
import Dashboard from "./components/Dashboard";
import FaceRecognition from "./components/FaceRecognition";
import LoginPage from "./components/LoginPage";
import LogoutButton from "./components/LogoutButton";
import RequireAuth from "./components/RequireAuth";
import "./App.css";

const App = () => {
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [loading, setLoading] = useState(true); // Indicates if the auth check is ongoing

  // Check authentication status on mount
  useEffect(() => {
    fetch(import.meta.env.VITE_API_BASE_URL + "api/auth/check", {
      method: "GET",
      credentials: "include", // Ensures JWT cookie is sent
    })
      .then((res) => res.json())
      .then((data) => {
        setIsAuthenticated(data.isAuthenticated);
        setLoading(false);
      })
      .catch(() => {
        setIsAuthenticated(false);
        setLoading(false);
      });
  }, []);

  return (
    <Router>
      <div className="app-container">
        <header className="app-header">
          <h1>Loic Portraits</h1>
          <div className="auth-controls">
            {isAuthenticated && (
              <LogoutButton onLogout={() => setIsAuthenticated(false)} />
            )}
          </div>
        </header>

        <div className="app-layout">
          <nav className="app-sidebar">
            <ul>
              <li>
                <NavLink to="/" className={({ isActive }) => (isActive ? "active-link" : "")}>
                  Home
                </NavLink>
              </li>
              <li>
                <NavLink to="/upload" className={({ isActive }) => (isActive ? "active-link" : "")}>
                  Upload
                </NavLink>
              </li>
              <li>
                <NavLink to="/search" className={({ isActive }) => (isActive ? "active-link" : "")}>
                  Search
                </NavLink>
              </li>
              <li>
                <NavLink
                  to="/naturallanguage"
                  className={({ isActive }) => (isActive ? "active-link" : "")}
                >
                  Natural Language Search
                </NavLink>
              </li>
              <li>
                <NavLink to="/dashboard" className={({ isActive }) => (isActive ? "active-link" : "")}>
                  Dashboard
                </NavLink>
              </li>
              <li>
                <NavLink
                  to="/facerecognition"
                  className={({ isActive }) => (isActive ? "active-link" : "")}
                >
                  Face Recognition
                </NavLink>
              </li>
            </ul>
          </nav>

          <main className="app-main">
            <Routes>
              {/* Public route for login */}
              <Route
                path="/login"
                element={<LoginPage onLoginSuccess={() => setIsAuthenticated(true)} />}
              />

              {/* All other routes are protected */}
              <Route element={<RequireAuth isAuthenticated={isAuthenticated} loading={loading} />}>
                <Route path="/" element={<Home />} />
                <Route path="/upload" element={<ImageUpload />} />
                <Route path="/search" element={<ImageSearch />} />
                <Route path="/naturallanguage" element={<NaturalLanguageSearch />} />
                <Route path="/dashboard" element={<Dashboard />} />
                <Route path="/facerecognition" element={<FaceRecognition />} />
              </Route>
            </Routes>
          </main>
        </div>

        <footer className="app-footer">
          <p>&copy; {new Date().getFullYear()} Loic Portraits LLC. All rights reserved.</p>
        </footer>
      </div>
    </Router>
  );
};

export default App;

