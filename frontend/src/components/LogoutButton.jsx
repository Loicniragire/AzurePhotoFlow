import React from 'react';
import { logout } from '../services/authApi';

const LogoutButton = ({ onLogout }) => {
  const handleLogout = async () => {
    try {
      const data = await logout();
      console.log("Logged out successfully:", data);
      onLogout();
    } catch (error) {
      console.error("Logout error:", error);
    }
  };

  return (
    <button
      onClick={handleLogout}
      style={{
        padding: "10px",
        backgroundColor: "#FF4D4D",
        color: "white",
        border: "none",
        cursor: "pointer",
        borderRadius: "5px",
        fontSize: "16px",
      }}
    >
      Logout
    </button>
  );
};

export default LogoutButton;

