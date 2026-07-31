# 0023 - Material and Product business data

- **Decision:** Material and Product follow the Machine vertical-slice boundary with separate CQRS handlers and feature-specific repositories.
- **Identity:** Codes are trimmed, normalized to uppercase, and unique within each company.
- **Material:** Unit remains a required short string in the MVP; a unit-of-measure catalog is intentionally deferred.
- **Product:** The MVP stores only code and name until Production Order proves another field is required.
- **API:** Both resources provide tenant-scoped list/search, create, update, and delete operations under the `Manager` policy.
- **Date:** 2026-08-01
