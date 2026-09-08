import { API_ROUTES } from './api.routes';

describe('API_ROUTES manufacturing parity', () => {
  it('builds production lifecycle and operation URLs', () => {
    expect(API_ROUTES.productionOrders.release('po-1')).toBe('/api/production-orders/po-1/release');
    expect(API_ROUTES.productionOrders.start('po-1')).toBe('/api/production-orders/po-1/start');
    expect(API_ROUTES.productionOrders.complete('po-1')).toBe(
      '/api/production-orders/po-1/complete',
    );
    expect(API_ROUTES.productionOrders.cancel('po-1')).toBe('/api/production-orders/po-1/cancel');
    expect(API_ROUTES.productionOrders.operations('po-1')).toBe(
      '/api/production-orders/po-1/operations',
    );
    expect(API_ROUTES.productionOrders.startOperation('po-1', 'op-1')).toBe(
      '/api/production-orders/po-1/operations/op-1/start',
    );
    expect(API_ROUTES.productionOrders.completeOperation('po-1', 'op-1')).toBe(
      '/api/production-orders/po-1/operations/op-1/complete',
    );
  });

  it('exposes read-only product inventory URLs', () => {
    expect(API_ROUTES.productInventories.root).toBe('/api/product-inventories');
    expect(API_ROUTES.productInventories.transactions).toBe(
      '/api/product-inventories/transactions',
    );
  });

  it('builds the Excel import template URL', () => {
    expect(API_ROUTES.excelImports.template('production_order')).toBe(
      '/api/imports/excel/template/production_order',
    );
  });
});
