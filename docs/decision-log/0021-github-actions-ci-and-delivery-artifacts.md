# 0021 - GitHub Actions CI and delivery artifacts

- **Decision:** Every branch push and pull request into `dev` runs independent backend and frontend GitHub Actions jobs.
- **Backend gate:** Restore, verify `dotnet format`, build Release with warnings as errors, run the complete test suite, and publish the API.
- **Frontend gate:** Install from `package-lock.json`, audit production dependencies, build the Angular production bundle, and run ChromeHeadless tests.
- **Delivery:** Successful runs upload API, frontend, and backend test-result artifacts with seven-day retention. This provides repeatable continuous-delivery inputs without claiming that an environment was deployed.
- **Security:** Workflow permissions are read-only, no application secrets are used, and concurrent obsolete runs on the same ref are cancelled.
- **Boundary:** Automatic deployment remains disabled until a target environment, secret-management policy, and rollback procedure are defined.
- **Date:** 2026-08-01
