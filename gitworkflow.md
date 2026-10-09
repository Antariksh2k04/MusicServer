# Git Workflow

## Branch naming
- Use `<type>/<app>/<short-description>`.
- If working on a GitLab issue, prefix the issue number in the short description without `#`:
  - `<type>/<app>/<issue-number>-<short-description>`.
  - Example: for GitLab issue `#123`, use `feature/frontend/123-add-course-filter`.
- Use lowercase and hyphens only.
- Keep the name short and clear.

## Branch types
- `feature`: New feature or improvement.
- `bugfix`: Normal development bug fix.
- `hotfix`: Urgent production or release-blocking fix.
- Discuss with team before creating a `hotfix` branch.

## App names
- Frontend app: `frontend`.
- Backend app: `backend`.
- Frontend and backend together: `fullstack`.

## Branch examples
### ElearningFrontend
- `feature/frontend/add-login-page`
- `bugfix/frontend/fix-course-card-layout`
- `hotfix/frontend/fix-auth-redirect`
- `feature/frontend/123-add-course-filter`
### ElearningBackend
- `feature/backend/add-course-api`
- `bugfix/backend/fix-user-enrollment`
- `hotfix/backend/fix-token-validation`
- `bugfix/backend/456-fix-enrollment-status`
### Full-stack changes
- `feature/fullstack/add-course-enrollment`
- `bugfix/fullstack/fix-course-enrollment-flow`
- `hotfix/fullstack/fix-login-outage`

## Branch name rules
- Good: `feature/frontend/add-course-filter`.
- Good with GitLab issue: `feature/frontend/123-add-course-filter`.
- Bad: `Feature/Frontend/Add Course Filter`.
- Bad: `feature/frontend/my-changes`.
- Bad: `bugfix/backend/fix-stuff`.

## Commit format
- Use Conventional Commits: `<type>(<scope>): <short summary>`.
- Example: `feat(frontend): add course filter`.
- Example: `fix(backend): handle empty enrollment list`.
- Example: `hotfix(auth): fix token validation`.

## Commit types
- `feat`: New feature.
- `fix`: Bug fix.
- `hotfix`: Urgent production or release fix.
- `docs`: Documentation only.
- `test`: Tests only.
- `refactor`: Code cleanup without behavior change.
- `chore`: Maintenance work.

## Commit scopes
- Use the smallest clear scope.
- Common scopes: `frontend`, `backend`, `auth`, `database`, `api`.
- Tips for choosing a scope:
  - Keep it short: Use clear, singular nouns (e.g., `auth` instead of `authentication-system`).

## Commit rules
- Use present tense.
- Keep summary under 60 characters when possible.
- Make one logical change per commit.
- Do not mix unrelated changes.
- Do not commit broken code.

## Minimal rules
- Create a branch before starting work.
- Pull latest branch before creating a branch.
- Discuss with team as it might depend on the task.
- Keep each branch focused on one task.
- Run relevant checks before pushing.
- Review your own diff before opening a pull request.
- Do not push directly to `main` or `dev` branches.

## Pull request checklist
- Branch name follows convention.
- Commit messages follow convention.
- Change is focused.
- Relevant checks completed.
- Review your own changes multiple times for typos and common mistakes.
- Migration notes added for database changes.
- Documentation updated if behavior changed.
