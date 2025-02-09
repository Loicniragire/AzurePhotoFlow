import React from "react";
import { Navigate, Outlet } from "react-router-dom";

const RequireAuth = ({ isAuthenticated, loading }) => {
  if (loading) {
    return <div>Loading authentication status...</div>;
  }
  return isAuthenticated ? <Outlet /> : <Navigate to="/login" replace />;
};

export default RequireAuth;

