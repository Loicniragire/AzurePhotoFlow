import { useEffect, useRef } from "react";
import { googleLogin } from "../services/authApi";

const GoogleLoginButton = ({ onLoginSuccess }) => {
  // Create a ref to hold the container element for the Google button.
  const buttonRef = useRef(null);

  useEffect(() => {
    const clientId = import.meta.env.VITE_GOOGLE_CLIENT_ID;
    console.log("Google Client ID:", clientId);
    if (!clientId) {
      console.error("Google Client ID is missing! Set it in the .env file.");
      return;
    }

    // Ensure that the container element is present.
    if (buttonRef.current) {
      // Initialize the Google Identity Services.
      window.google.accounts.id.initialize({
        client_id: clientId,
        callback: async (response) => {
          console.log("Google Login Response:", response);
          console.log("Credential received:", response.credential);

          try {
            // Use the API module to perform Google login.
            const data = await googleLogin(response.credential);
            console.log("Login successful:", data);
            onLoginSuccess();
          } catch (err) {
            console.error("Login error:", err);
          }
        },
      });

      // Render the Google Sign-In button into the container.
      window.google.accounts.id.renderButton(buttonRef.current, {
        theme: "outline",
        size: "large",
      });
    }
  }, [onLoginSuccess]);

  return (
    <div>
      {/* Always render the container element */}
      <div ref={buttonRef} id="googleSignInButton"></div>
    </div>
  );
};

export default GoogleLoginButton;

