import createClient from "openapi-fetch";
import type { paths } from "./generated/api-schema";

// Use relative URL in production (same origin), absolute in dev
const baseUrl = import.meta.env.VITE_API_BASE_URL ||
  (import.meta.env.PROD ? "" : "http://localhost:5054/");

// Get admin key at load time (from localStorage or env var)
const adminKey = localStorage.getItem('adminKey') || import.meta.env.VITE_ADMIN_KEY;

const client = createClient<paths>({ 
  baseUrl,
  // Include admin key in headers if available
  ...(adminKey && { headers: { 'X-Admin-Key': adminKey } })
});

export default client;