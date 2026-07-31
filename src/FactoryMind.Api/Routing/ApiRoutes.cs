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
    }

    public static class Inventories {
        public const string Group = Base + "/inventories";
        public const string Root = "";
        public const string ById = "/{inventoryId:guid}";
    }

    public static class ProductionOrders {
        public const string Group = Base + "/production-orders";
        public const string Root = "";
        public const string ById = "/{productionOrderId:guid}";
    }
}
