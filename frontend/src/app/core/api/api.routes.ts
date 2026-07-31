const API_BASE = '/api';

export const API_ROUTES = {
  auth: {
    login: `${API_BASE}/auth/login`,
    refresh: `${API_BASE}/auth/refresh`,
    logout: `${API_BASE}/auth/logout`,
  },
  conversations: {
    root: `${API_BASE}/conversations`,
    messages: (conversationId: string) =>
      `${API_BASE}/conversations/${conversationId}/messages`,
    streamMessage: (conversationId: string) =>
      `${API_BASE}/conversations/${conversationId}/messages/stream`,
  },
} as const;
