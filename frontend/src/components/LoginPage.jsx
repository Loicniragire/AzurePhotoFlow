import React from "react";
import { useNavigate } from "react-router-dom";
import GoogleLoginButton from "./GoogleLoginButton";

const LoginPage = ({ onLoginSuccess }) => {
  const navigate = useNavigate();

  const handleLoginSuccess = () => {
    onLoginSuccess();
    // Redirect to the default landing page after successful login
    navigate("/", { replace: true });
  };

  return (
    <div className="login-page">
      <h2>Please log in to continue</h2>
      <GoogleLoginButton onLoginSuccess={handleLoginSuccess} />
    </div>
  );
};

export default LoginPage;

