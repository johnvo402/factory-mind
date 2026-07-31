# 0013 - PDF upload to MinIO

- **Decision:** Sprint 3 starts with an authenticated PDF upload/list vertical slice before parsing, chunking, and embedding.
- **Decision:** PDF bytes are stored in MinIO through an Application storage abstraction; PostgreSQL stores tenant-scoped document metadata and the generated object key only.
- **Security:** Accept only non-empty `.pdf` uploads up to 100 MB with `application/pdf` content type and a valid `%PDF-` signature.
- **Tenant boundary:** Object keys begin with the authenticated `CompanyId`, and document repository reads require `CompanyId`.
- **Consistency:** Upload the object before saving metadata. A later cleanup job removes rare orphan objects if database persistence fails; no distributed transaction is introduced.
- **Boundary:** PDF parsing, chunks, embeddings, search, citations, DOCX, and XLSX remain outside this slice.
- **Local runtime:** Pin the last published official MinIO Docker Hub image and bind its API/console ports to `127.0.0.1`. Reassess the maintained production object-storage image before deployment.
- **Date:** 2026-07-31
