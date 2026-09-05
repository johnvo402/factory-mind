namespace FactoryMind.Api.Routing;

public static class ApiRoutes {
    public const string Base = "/api";
    public const string Health = "/health";

    public static class Auth {
        public const string Group = Base + "/auth";
        public const string Login = "/login";
        public const string Refresh = "/refresh";
        public const string Logout = "/logout";
    }

    public static class Conversations {
        public const string Group = Base + "/conversations";
        public const string Root = "";
        public const string Messages = "/{conversationId:guid}/messages";
        public const string StreamMessage = Messages + "/stream";
    }

    public static class Documents {
        public const string Group = Base + "/documents";
        public const string Root = "";
        public const string Process = "/{documentId:guid}/process";
        public const string Reindex = "/reindex";
    }

    public static class Dashboard {
        public const string Group = Base + "/dashboard";
        public const string Summary = "/summary";
    }

    public static class ExcelImports {
        public const string Group = Base + "/imports/excel";
        public const string Preview = "/preview";
        public const string Import = "/import";
    }

    public static class Settings {
        public const string Group = Base + "/settings";
        public const string Company = "/company";
        public const string Users = "/users";
        public const string UserById = Users + "/{userId:guid}";
        public const string Ai = "/ai";
    }

    public static class Knowledge {
        public const string Group = Base + "/knowledge";
        public const string Search = "/search";
    }

    public static class Machines {
        public const string Group = Base + "/machines";
        public const string Root = "";
        public const string ById = "/{machineId:guid}";
    }

    public static class Materials {
        public const string Group = Base + "/materials";
        public const string Root = "";
        public const string ById = "/{materialId:guid}";
    }

    public static class Products {
        public const string Group = Base + "/products";
        public const string Root = "";
        public const string ById = "/{productId:guid}";
        public const string Boms = ById + "/boms";
        public const string BomById = Boms + "/{bomId:guid}";
        public const string ActivateBom = BomById + "/activate";
        public const string ArchiveBom = BomById + "/archive";
        public const string MaterialRequirements = ById + "/material-requirements";
        public const string Routings = ById + "/routings";
        public const string RoutingById = Routings + "/{routingId:guid}";
        public const string ActivateRouting = RoutingById + "/activate";
    }

    public static class Inventories {
        public const string Group = Base + "/inventories";
        public const string Root = "";
        public const string Transactions = "/transactions";
        public const string Receive = "/receive";
        public const string Issue = "/issue";
        public const string Adjust = "/adjust";
        public const string Transfer = "/transfer";
    }

    public static class ProductInventories {
        public const string Group = Base + "/product-inventories";
        public const string Root = "";
        public const string Transactions = "/transactions";
    }

    public static class Warehouses {
        public const string Group = Base + "/warehouses";
        public const string Root = "";
        public const string ById = "/{warehouseId:guid}";
    }

    public static class WorkCenters {
        public const string Group = Base + "/work-centers";
        public const string Root = "";
        public const string ById = "/{workCenterId:guid}";
        public const string Deactivate = ById + "/deactivate";
    }

    public static class ProductionOrders {
        public const string Group = Base + "/production-orders";
        public const string Root = "";
        public const string ById = "/{productionOrderId:guid}";
        public const string Release = ById + "/release";
        public const string Start = ById + "/start";
        public const string Complete = ById + "/complete";
        public const string Cancel = ById + "/cancel";
        public const string MaterialRequirements = ById + "/material-requirements";
        public const string Operations = ById + "/operations";
        public const string OperationById = Operations + "/{operationId:guid}";
        public const string StartOperation = OperationById + "/start";
        public const string CompleteOperation = OperationById + "/complete";
    }
}
