export interface DashboardSummary {
  activeOrders: number;
  inventoryBalances: number;
  availableMachines: number;
  totalMachines: number;
  alerts: number;
  overdueOrders: number;
  dueSoonOrders: number;
  urgentActiveOrders: number;
  completedLateOrders: number;
  ordersWithoutDueDate: number;
}
