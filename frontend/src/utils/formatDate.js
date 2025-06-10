export function formatDate(dateString) {
  const d = new Date(dateString);
  if (isNaN(d)) {
    return null;
  }
  return d.toISOString().split('T')[0];
}
