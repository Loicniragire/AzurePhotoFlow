const LogoutButton = () => {
  const handleLogout = () => {
    fetch(import.meta.env.VITE_API_BASE_URL + "/auth/logout", {
      method: "POST",
      credentials: "include", // Ensure cookies are sent
    })
      .then(() => {
        console.log("Logged out successfully!");
        window.location.reload(); // Refresh to reset session
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

