import { useEffect, useState } from "react";

const GoogleLoginButton = ({ onLoginSuccess }) => {
  const [isGoogleLoaded, setIsGoogleLoaded] = useState(false);

  useEffect(() => {
    const clientId = import.meta.env.VITE_GOOGLE_CLIENT_ID;

    if (!clientId) {
      console.error("Google Client ID is missing! Set it in .env file.");
      return;
    }

    // Load Google Identity Services
    window.google.accounts.id.initialize({
      client_id: clientId,
      callback: (response) => {
        console.log("Google Login Response:", response);
        fetch(import.meta.env.VITE_API_BASE_URL + "/auth/google-login", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ token: response.credential }),
          credentials: "include",
        })
          .then((res) => res.json())
          .then((data) => {
            console.log("Login successful:", data);
            onLoginSuccess();
          })
          .catch((err) => console.error("Login error:", err));
      },
    });

    window.google.accounts.id.renderButton(
      document.getElementById("googleSignInButton"),
      { theme: "outline", size: "large" }
    );

    setIsGoogleLoaded(true);
  }, [onLoginSuccess]);

  return (
    <div>
      {isGoogleLoaded ? (
        <div id="googleSignInButton"></div>
      ) : (
        <p style={{ color: "red" }}>Error loading Google Sign-In</p>
      )}
    </div>
  );
};

export default GoogleLoginButton;

