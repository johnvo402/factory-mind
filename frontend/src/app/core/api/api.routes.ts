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
  dashboard: {
    summary: `${API_BASE}/dashboard/summary`,
  },
  machines: {
    root: `${API_BASE}/machines`,
    byId: (machineId: string) => `${API_BASE}/machines/${machineId}`,
  },
  materials: {
    root: `${API_BASE}/materials`,
    byId: (materialId: string) => `${API_BASE}/materials/${materialId}`,
  },
  products: {
    root: `${API_BASE}/products`,
    byId: (productId: string) => `${API_BASE}/products/${productId}`,
  },
  inventories: {
    root: `${API_BASE}/inventories`,
    byId: (inventoryId: string) => `${API_BASE}/inventories/${inventoryId}`,
  },
  productionOrders: {
    root: `${API_BASE}/production-orders`,
    byId: (productionOrderId: string) => `${API_BASE}/production-orders/${productionOrderId}`,
  },
} as const;
