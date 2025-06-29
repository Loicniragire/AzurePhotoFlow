import React, { useState, useEffect } from "react";
import { BrowserRouter as Router, Routes, Route, NavLink, Navigate } from "react-router-dom";
import {
  AppBar,
  Toolbar,
  Typography,
  IconButton,
  Drawer,
  List,
  ListItem,
  ListItemText,
  CssBaseline,
  useTheme,
  useMediaQuery,
  Container,
} from "@mui/material";
import MenuIcon from "@mui/icons-material/Menu";
import { QueryClient, QueryClientProvider } from "react-query"; 
import Home from "./components/Home";
import ImageUpload from "./components/ImageUpload";
import ImageSearchNew from "./components/ImageSearch";
import Dashboard from "./components/Dashboard";
import FaceRecognition from "./components/FaceRecognition";
import LoginPage from "./components/LoginPage";
import LogoutButton from "./components/LogoutButton";
import RequireAuth from "./components/RequireAuth";
import jwt_decode from "jwt-decode";

const drawerWidth = 240;

// Create a QueryClient instance
const queryClient = new QueryClient();

const App = () => {
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [loading, setLoading] = useState(true); // Indicates if the auth check is ongoing
  const [mobileOpen, setMobileOpen] = useState(false);

  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));

  // Check authentication status on mount
  useEffect(() => {
	  const getTokenStatus = () => {
		const token = localStorage.getItem("jwtToken");
		if (!token) {
		  return { isAuthenticated: false };
		}
		try {
		  const decoded = jwt_decode(token);
		  // Check if token has expired
		  if (decoded.exp * 1000 < Date.now()) {
			localStorage.removeItem("jwtToken");
			return { isAuthenticated: false };
		  }
		  return { isAuthenticated: true, decoded };
		} catch (error) {
		  console.error("Token decoding failed:", error);
		  localStorage.removeItem("jwtToken");
		  return { isAuthenticated: false };
		}
	  };

	  // 1. Fetch the status
	  const status = getTokenStatus();

	  // 2. Update the component state
	  setIsAuthenticated(status.isAuthenticated);

	  // 3. Signal that loading is complete
	  setLoading(false);

	}, []);

  const handleDrawerToggle = () => {
    setMobileOpen(!mobileOpen);
  };

  // Define the navigation items
  const navItems = [
    { text: "Home", path: "/" },
    { text: "Upload", path: "/upload" },
    { text: "Search", path: "/search" },
    { text: "Dashboard", path: "/dashboard" },
    { text: "Face Recognition", path: "/facerecognition" },
  ];

  const drawer = (
    <div>
      <List>
        {navItems.map((item) => (
          <ListItem
            button
            key={item.text}
            component={NavLink}
            to={item.path}
            onClick={isMobile ? handleDrawerToggle : undefined}
          >
            <ListItemText primary={item.text} />
          </ListItem>
        ))}
      </List>
    </div>
  );

  return (
    // Wrap the entire application with QueryClientProvider for caching support
    <QueryClientProvider client={queryClient}>
      <Router>
        <CssBaseline />
        <div style={{ display: "flex" }}>
          {/* AppBar as the modern banner */}
          <AppBar position="fixed" style={{ zIndex: theme.zIndex.drawer + 1 }}>
            <Toolbar>
              {isMobile && (
                <IconButton
                  color="inherit"
                  aria-label="open drawer"
                  edge="start"
                  onClick={handleDrawerToggle}
                  style={{ marginRight: theme.spacing(2) }}
                >
                  <MenuIcon />
                </IconButton>
              )}
              <Typography variant="h6" noWrap component="div">
                Photo Flow
              </Typography>
              <div style={{ flexGrow: 1 }} />
              {isAuthenticated && (
                <LogoutButton onLogout={() => setIsAuthenticated(false)} />
              )}
            </Toolbar>
          </AppBar>

          {/* Responsive Drawer Navigation */}
          {isMobile ? (
            <Drawer
              variant="temporary"
              open={mobileOpen}
              onClose={handleDrawerToggle}
              ModalProps={{
                keepMounted: true, // Better performance on mobile.
              }}
              sx={{
                "& .MuiDrawer-paper": { boxSizing: "border-box", width: drawerWidth },
              }}
            >
              {drawer}
            </Drawer>
          ) : (
            <Drawer
              variant="permanent"
              sx={{
                width: drawerWidth,
                flexShrink: 0,
                "& .MuiDrawer-paper": { width: drawerWidth, boxSizing: "border-box" },
              }}
            >
              <Toolbar />
              {drawer}
            </Drawer>
          )}

          {/* Main content area */}
          <main style={{ flexGrow: 1, padding: theme.spacing(3), marginTop: theme.spacing(8) }}>
            <Container>
              <Routes>
                <Route
                  path="/login"
                  element={<LoginPage onLoginSuccess={() => setIsAuthenticated(true)} />}
                />
                <Route element={<RequireAuth isAuthenticated={isAuthenticated} loading={loading} />}>
                  <Route path="/" element={<Home />} />
                  <Route path="/upload" element={<ImageUpload />} />
                  <Route path="/search" element={<ImageSearchNew />} />
				  <Route path="/Search" element={<Navigate to="/search" replace />} />
                  <Route path="/dashboard" element={<Dashboard />} />
                  <Route path="/facerecognition" element={<FaceRecognition />} />
                </Route>
              </Routes>
            </Container>
          </main>
        </div>
        <footer style={{ textAlign: "center", padding: theme.spacing(2) }}>
          <Typography variant="body2" color="textSecondary">
            &copy; {new Date().getFullYear()} Photo Flow. All rights reserved.
          </Typography>
        </footer>
      </Router>
    </QueryClientProvider>
  );
};

export default App;

