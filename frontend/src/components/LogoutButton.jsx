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
    <button onClick={handleLogout} style={{ padding: "10px", cursor: "pointer" }}>
      Logout
    </button>
  );
};

export default LogoutButton;

