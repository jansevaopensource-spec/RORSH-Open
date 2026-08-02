# Contributing to RORSH

Thank you for your interest in contributing to RORSH! This document provides guidelines for contributing.

## Code of Conduct

Be respectful and constructive in all interactions.

## How to Contribute

### Reporting Bugs

1. Check if the issue already exists
2. Open a new issue with:
   - Clear title and description
   - Steps to reproduce
   - Expected vs actual behavior
   - Environment details (OS, .NET version, etc.)

### Suggesting Features

Open an issue with the `enhancement` label describing:
- The feature and its use case
- Proposed implementation approach

### Pull Requests

1. Fork the repository
2. Create a feature branch from `RORSH-Com`:
   ```bash
   git checkout -b feature/your-feature-name
   ```
3. Make your changes
4. Test thoroughly
5. Commit with clear messages:
   ```bash
   git commit -m "feat: Add new feature"
   git commit -m "fix: Resolve connection timeout"
   git commit -m "docs: Update README"
   ```
6. Push and open a PR against `RORSH-Com`

## Commit Message Convention

- `feat:` New feature
- `fix:` Bug fix
- `docs:` Documentation changes
- `style:` Code style changes (formatting, etc.)
- `refactor:` Code refactoring
- `perf:` Performance improvements
- `test:` Test changes
- `chore:` Build/config changes

## Development Setup

### Prerequisites
- .NET 8.0 SDK
- Node.js 18+
- Git

### Running Locally

**Server:**
```bash
npm install
npm start
```

**Admin Shell:**
```bash
cd RAS/RORSHTerminal
dotnet run
```

**Client Shell:**
```bash
cd RCS/RORSHClient
dotnet run
```

## Security

For security issues, use [GitHub Security Advisories](https://github.com/jansevaopensource-spec/RORSH-Open/security/advisories) instead of public issues.

## License

By contributing, you agree that your contributions will be licensed under the Apache License 2.0.
