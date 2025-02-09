const LogoutButton = ({ onLogout }) => {
  const handleLogout = () => {
    fetch(import.meta.env.VITE_API_BASE_URL + "/auth/logout", {
      method: "POST",
      credentials: "include",
    })
      .then(() => {
        console.log("Logged out successfully!");
        onLogout();
      })
      .catch((err) => console.error("Logout error:", err));
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

