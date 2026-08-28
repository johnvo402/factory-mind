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
  excelImports: {
    preview: `${API_BASE}/imports/excel/preview`,
    import: `${API_BASE}/imports/excel/import`,
  },
  settings: {
    company: `${API_BASE}/settings/company`,
    users: `${API_BASE}/settings/users`,
    userById: (userId: string) => `${API_BASE}/settings/users/${userId}`,
    ai: `${API_BASE}/settings/ai`,
  },
  documents: {
    root: `${API_BASE}/documents`,
    process: (documentId: string) => `${API_BASE}/documents/${documentId}/process`,
    reindex: `${API_BASE}/documents/reindex`,
  },
  knowledge: {
    search: `${API_BASE}/knowledge/search`,
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
    transactions: `${API_BASE}/inventories/transactions`,
    receive: `${API_BASE}/inventories/receive`,
    issue: `${API_BASE}/inventories/issue`,
    adjust: `${API_BASE}/inventories/adjust`,
    transfer: `${API_BASE}/inventories/transfer`,
  },
  warehouses: {
    root: `${API_BASE}/warehouses`,
    byId: (warehouseId: string) => `${API_BASE}/warehouses/${warehouseId}`,
  },
  productionOrders: {
    root: `${API_BASE}/production-orders`,
    byId: (productionOrderId: string) => `${API_BASE}/production-orders/${productionOrderId}`,
  },
} as const;
