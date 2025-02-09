import { useEffect } from "react";

const GoogleLoginButton = ({ onLoginSuccess }) => {
  useEffect(() => {
    const clientId = import.meta.env.VITE_GOOGLE_CLIENT_ID;

    if (!clientId) {
      console.error("Google Client ID is missing! Set it in .env file.");
      return;
    }

    window.google.accounts.id.initialize({
      client_id: clientId,
      callback: (response) => {
        fetch(import.meta.env.VITE_API_BASE_URL + "/auth/google-login", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ token: response.credential }),
          credentials: "include",
        })
          .then((res) => res.json())
          .then((data) => {
            console.log("Login successful");
            onLoginSuccess();
          })
          .catch((err) => console.error("Login error:", err));
      },
    });

    window.google.accounts.id.renderButton(
      document.getElementById("googleSignInButton"),
      { theme: "outline", size: "large" }
    );
  }, [onLoginSuccess]);

  return <div id="googleSignInButton"></div>;
};

export default GoogleLoginButton;

