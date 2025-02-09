import { useEffect } from "react";

const GoogleLoginButton = () => {
  useEffect(() => {
    const clientId = import.meta.env.VITE_GOOGLE_CLIENT_ID;
    
    if (!clientId) {
      console.error("Google Client ID is missing! Make sure it's set in .env file.");
      return;
    }

    window.google.accounts.id.initialize({
      client_id: clientId,
      callback: handleCredentialResponse,
    });

    window.google.accounts.id.renderButton(
      document.getElementById("googleSignInButton"),
      { theme: "outline", size: "large" }
    );
  }, []);

  const handleCredentialResponse = (response) => {
    console.log("Google JWT ID Token:", response.credential);
    
    fetch(import.meta.env.VITE_API_BASE_URL + "/auth/google-login", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ token: response.credential }),
	  credentials: "include"
    })
      .then((res) => res.json())
      .then((data) => console.log("Backend Response:", data))
      .catch((err) => console.error("Error:", err));
  };

  return <div id="googleSignInButton"></div>;
};

export default GoogleLoginButton;

