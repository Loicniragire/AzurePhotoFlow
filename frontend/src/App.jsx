import React, { useState, useEffect } from "react";
import { BrowserRouter as Router, Routes, Route, NavLink } from "react-router-dom";
import ImageUpload from "./components/ImageUpload";
import ImageSearch from "./components/ImageSearch";
import NaturalLanguageSearch from "./components/NaturalLanguageSearch";
import Dashboard from "./components/Dashboard";
import FaceRecognition from "./components/FaceRecognition";
import Home from "./components/Home";
import GoogleLoginButton from "./components/GoogleLoginButton"; // Import Login Button
import LogoutButton from "./components/LogoutButton"; // Import Logout Button
import "./App.css";

const App = () => {
  const [isSidebarOpen, setIsSidebarOpen] = useState(false);
  const [isAuthenticated, setIsAuthenticated] = useState(false);

  // Check authentication status
  useEffect(() => {
    fetch(import.meta.env.VITE_API_BASE_URL + "/auth/check", {
      method: "GET",
      credentials: "include", // Required to send cookies
    })
      .then((res) => res.json())
      .then((data) => setIsAuthenticated(data.isAuthenticated))
      .catch(() => setIsAuthenticated(false));
  }, []);

  const toggleSidebar = () => {
    setIsSidebarOpen(!isSidebarOpen);
  };

  const closeSidebar = () => {
    setIsSidebarOpen(false);
  };

  return (
    <Router>
      <div className="app-container">
        <header className="app-header">
          <h1>Loic Portraits</h1>
          <button className="hamburger-button" onClick={toggleSidebar}>
            &#9776;
          </button>
        </header>
        <div className="app-layout">
          <nav className={`app-sidebar ${isSidebarOpen ? "open" : ""}`}>
            <ul>
              <li>
                <NavLink
                  to="/upload"
                  className={({ isActive }) => (isActive ? "active-link" : "")}
                  onClick={closeSidebar}
                >
                  Upload
                </NavLink>
              </li>
              <li>
                <NavLink
                  to="/search"
                  className={({ isActive }) => (isActive ? "active-link" : "")}
                  onClick={closeSidebar}
                >
                  Search
                </NavLink>
              </li>
              <li>
                <NavLink
                  to="/naturallanguage"
                  className={({ isActive }) => (isActive ? "active-link" : "")}
                  onClick={closeSidebar}
                >
                  Natural Language Search
                </NavLink>
              </li>
              <li>
                <NavLink
                  to="/dashboard"
                  className={({ isActive }) => (isActive ? "active-link" : "")}
                  onClick={closeSidebar}
                >
                  Dashboard
                </NavLink>
              </li>
              <li>
                <NavLink
                  to="/facerecognition"
                  className={({ isActive }) => (isActive ? "active-link" : "")}
                  onClick={closeSidebar}
                >
                  Face Recognition
                </NavLink>
              </li>
            </ul>

            {/* Authentication Section */}
            <div className="auth-section">
              {isAuthenticated ? (
                <LogoutButton onLogout={() => setIsAuthenticated(false)} />
              ) : (
                <GoogleLoginButton onLoginSuccess={() => setIsAuthenticated(true)} />
              )}
            </div>
          </nav>

          {isSidebarOpen && <div className="overlay" onClick={closeSidebar}></div>}

          <main className="app-main">
            <Routes>
              <Route path="/upload" element={<ImageUpload />} />
              <Route path="/search" element={<ImageSearch />} />
              <Route path="/naturallanguage" element={<NaturalLanguageSearch />} />
              <Route path="/dashboard" element={<Dashboard />} />
              <Route path="/facerecognition" element={<FaceRecognition />} />
              <Route path="*" element={<Home />} />
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

