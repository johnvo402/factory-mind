using FactoryMind.Application.Common.Search;
using FactoryMind.Domain.Manufacturing;

namespace FactoryMind.Application.Features.Chat.Rag;

public sealed class IntentRouter : IIntentRouter {
    private static readonly string[] KnowledgeKeywords = [
        "sop", "manual", "huong dan", "quy trinh", "tieu chuan", "iso", "qc",
        "tai lieu", "an toan", "chinh sach", "policy"
    ];

    private static readonly string[] DecisionKeywords = [
        "co nen", "nen nhan", "goi y", "de xuat", "khuyen nghi", "recommend"
    ];

    private static readonly IReadOnlyDictionary<BusinessDataScope, string[]> BusinessKeywords =
        new Dictionary<BusinessDataScope, string[]> {
            [BusinessDataScope.Machines] = [
                "may", "machine", "thiet bi", "bao tri", "maintenance", "available", "running"
            ],
            [BusinessDataScope.Materials] = [
                "nguyen lieu", "vat lieu", "vat tu", "material", "bom", "dinh muc"
            ],
            [BusinessDataScope.Inventory] = [
                "kho", "ton", "inventory", "stock", "warehouse", "thanh pham", "finished goods"
            ],
            [BusinessDataScope.Products] = [
                "san pham", "product", "bom", "dinh muc", "cau tao", "lam tu"
            ],
            [BusinessDataScope.ProductionOrders] = [
                "lenh san xuat", "don hang", "production order", "order", "tien do", "po"
            ],
            [BusinessDataScope.WorkCenters] = [
                "work center", "workcenter", "trung tam gia cong", "station", "line"
            ],
            [BusinessDataScope.Routings] = [
                "routing", "route", "quy trinh san xuat", "lo trinh"
            ],
            [BusinessDataScope.ProductionOperations] = [
                "cong doan", "operation", "production operation", "current operation",
                "next operation", "may dang lam lenh nao", "dang chay lenh nao"
            ]
        };

    public IntentRoute Route(string question) {
        var normalized = SearchTextNormalizer.Normalize(question);
        var words = normalized.Split([' ', '-'], StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var hasKnowledgeIntent = KnowledgeKeywords.Any(keyword => ContainsKeyword(normalized, words, keyword));
        var hasDecisionIntent = DecisionKeywords.Any(keyword => ContainsKeyword(normalized, words, keyword));
        var machineStatus = DetectMachineStatus(normalized, words);
        var productionOrderStatus = DetectProductionOrderStatus(normalized, words);
        var scopes = BusinessDataScope.None;

        foreach (var pair in BusinessKeywords) {
            if (pair.Value.Any(keyword => ContainsKeyword(normalized, words, keyword))) {
                scopes |= pair.Key;
            }
        }

        var identifiers = SearchTextNormalizer.Identifiers(question);
        if (identifiers.Any(identifier => identifier.StartsWith("po-", StringComparison.Ordinal))) {
            scopes |= BusinessDataScope.ProductionOrders | BusinessDataScope.ProductionOperations;
        }

        if (scopes.HasFlag(BusinessDataScope.Machines)
            && ContainsAny(normalized, words, "dang chay", "dang lam", "lenh nao", "current")) {
            scopes |= BusinessDataScope.ProductionOperations;
        }

        if (identifiers.Any(identifier => !identifier.StartsWith("po-", StringComparison.Ordinal))
            && ContainsAny(normalized, words, "dang chay", "dang lam", "running", "current operation")) {
            scopes |= BusinessDataScope.Machines;
        }

        if (scopes.HasFlag(BusinessDataScope.ProductionOperations)) {
            scopes |= BusinessDataScope.ProductionOrders;
        }

        if ((hasKnowledgeIntent || hasDecisionIntent) && scopes != BusinessDataScope.None) {
            return new IntentRoute(ChatIntent.Hybrid, scopes, machineStatus, productionOrderStatus);
        }

        if (hasKnowledgeIntent) {
            return new IntentRoute(ChatIntent.Knowledge, BusinessDataScope.None);
        }

        if (scopes != BusinessDataScope.None) {
            return new IntentRoute(ChatIntent.Business, scopes, machineStatus, productionOrderStatus);
        }

        return new IntentRoute(ChatIntent.Hybrid, BusinessDataScope.All, IsFallback: true);
    }

    private static bool ContainsKeyword(string text, IReadOnlySet<string> words, string keyword) =>
        keyword.Contains(' ', StringComparison.Ordinal)
            ? text.Contains(keyword, StringComparison.Ordinal)
            : words.Contains(keyword);

    private static string? DetectMachineStatus(string text, IReadOnlySet<string> words) {
        if (ContainsAny(text, words, "ranh", "available", "san sang")) {
            return MachineStatuses.Available;
        }

        if (ContainsAny(text, words, "dang chay", "running")) {
            return MachineStatuses.Running;
        }

        if (ContainsAny(text, words, "bao tri", "maintenance")) {
            return MachineStatuses.Maintenance;
        }

        return ContainsAny(text, words, "offline", "ngoai tuyen")
            ? MachineStatuses.Offline
            : null;
    }

    private static string? DetectProductionOrderStatus(string text, IReadOnlySet<string> words) {
        if (ContainsAny(text, words, "dang san xuat", "in progress")) {
            return ProductionOrderStatuses.InProgress;
        }

        if (ContainsAny(text, words, "hoan thanh", "completed")) {
            return ProductionOrderStatuses.Completed;
        }

        if (ContainsAny(text, words, "huy", "cancelled", "canceled")) {
            return ProductionOrderStatuses.Cancelled;
        }

        return ContainsAny(text, words, "ke hoach", "planned")
            ? ProductionOrderStatuses.Planned
            : null;
    }

    private static bool ContainsAny(
        string text,
        IReadOnlySet<string> words,
        params string[] keywords) => keywords.Any(keyword => ContainsKeyword(text, words, keyword));

}
