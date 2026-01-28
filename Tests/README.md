# Tests — Developer README

Purpose
- Contains unit and integration tests for SkyCMS server-side logic.

Quick start
```powershell
dotnet test Tests\Sky.Tests.csproj
```

Where to look
- Individual test classes: `Tests/*`.
- Shared test utilities: `Sky.TestSetup` project.

Notes
- Tests are run in CI; aim to keep tests deterministic and fast.
- If adding new code, add tests in the appropriate test project and run `dotnet test` locally before pushing.
# Sky.Tests — SkyCMS Test Suite

Comprehensive test suite for the SkyCMS platform covering Editor functionality, security, authentication, authorization, and multi-database support.

## Table of Contents

- [Overview](#overview)
- [Prerequisites](#prerequisites)
- [Getting Started](#getting-started)
- [Test Configuration](#test-configuration)
- [Test Structure](#test-structure)
- [Running Tests](#running-tests)
- [Database Provider Testing](#database-provider-testing)
- [Test Categories](#test-categories)
- [Writing New Tests](#writing-new-tests)
- [Troubleshooting](#troubleshooting)

---

## Overview

**Project:** `Sky.Tests.csproj`  
**Framework:** MSTest  
**Target:** .NET 9.0  
**Key Dependencies:**
- `Microsoft.NET.Test.Sdk`
- `MSTest.TestAdapter` / `MSTest.TestFramework`
- `Microsoft.EntityFrameworkCore.InMemory`
- `Moq` (mocking framework)
- `RazorLight` (template rendering)

The test suite uses **in-memory databases** for fast, isolated test execution without external dependencies.

---

## Prerequisites

- **.NET 9.0 SDK** or higher
- **Visual Studio 2022** (17.8+) or **Visual Studio Code** with C# extension
- *Optional:* Azure Cosmos DB Emulator, SQL Server, or MySQL for database provider testing

---

## Getting Started

### Clone and Restore

```bash
git clone https://github.com/your-repo/SkyCMS.git
cd SkyCMS
dotnet restore
```

### Build and Launch Tests

```bash
# Build the solution
dotnet build

# Run the test suite
dotnet test
```

### Run Specific Test Projects or Classes

To run tests for a specific project or filter by class/method name:

```bash
# Run a specific test project
dotnet test ./Tests/Sky.Tests.csproj

# Filter by class
dotnet test ./Tests/Sky.Tests.csproj --filter "NamespaceOfYourTestClass"

# Filter by method
dotnet test ./Tests/Sky.Tests.csproj --filter "FullyQualifiedName~NameOfYourTestMethod"
```

---

## Test Configuration

- **In-memory databases** are configured by default for speed
- Connection strings and other settings are managed via `appsettings.json` in the test projects
- Use `UserSecrets` or environment variables for sensitive data (e.g., connection strings)

---

## Test Structure

Tests are organized by functionality and feature area. Key folders/files include:

- `ArticleEditLogicTests.cs`: Core functionality for article editing logic
- `PublishingServiceTests.cs`: Tests for the publishing service
- `ReservedPathsTests.cs`: Reserved paths service tests
- `BlogControllerTests.cs`: Blog controller tests

Each test file corresponds to a specific component or service in the SkyCMS platform.

---

## Running Tests

Execute tests from the command line or via an IDE:

```bash
# Run all tests
dotnet test

# Run tests in a specific project only
dotnet test ./Tests/Sky.Tests.csproj

# Run tests with filtering by class or method name
dotnet test ./Tests/Sky.Tests.csproj --filter "FullyQualifiedName~YourFilter"
```

### Using Visual Studio

- Open the solution in Visual Studio
- Build the solution (`Ctrl+Shift+B`)
- Open the Test Explorer (`Test > Test Explorer`)
- Run tests directly from the Test Explorer interface

---

## Database Provider Testing

### SQL Server

For SQL Server testing, ensure the following:

- Local SQL Server instance is running
- Connection string is set in `appsettings.json` or via UserSecrets

Run tests with the SQL Server configuration:

```bash
dotnet test ./Tests/Sky.Tests.csproj --configuration Release
```

### MySQL

For MySQL testing:

- MySQL server is running and accessible
- Connection string configured for MySQL in `appsettings.json`

Execute MySQL specific tests:

```bash
dotnet test ./Tests/Sky.Tests.csproj --configuration Release --filter "MySql"
```

---

## Test Categories

Tests are divided into categories for easier management and execution:

- **Smoke Tests:** Basic functionality checks, fast to execute
- **Regression Tests:** Verify previously fixed issues do not reoccur
- **Integration Tests:** Check interactions between components/services
- **Load Tests:** Assess performance under load

Run tests by category using the `--filter` option with traits:

```bash
dotnet test ./Tests/Sky.Tests.csproj --filter "Category=Smoke"
```

---

## Writing New Tests

To add new tests:

1. Identify the feature area and locate the corresponding test file or create a new one
2. Follow the existing test patterns and structure
3. Use descriptive names for test methods:
   - `MethodName_Condition_ExpectedBehavior`
4. Keep tests isolated and deterministic
5. If testing UI or controller logic, consider using test doubles (mocks/stubs) for dependencies

Example of a simple test method:

```csharp
[TestMethod]
public void CreateArticle_ValidData_ArticleIsCreated()
{
    // Arrange
    var articleLogic = new ArticleEditLogic(...);
    var articleData = new Article { Title = "Test Article", Content = "Test content." };

    // Act
    var result = articleLogic.CreateArticle(articleData);

    // Assert
    Assert.IsNotNull(result);
    Assert.AreEqual("Test Article", result.Title);
    Assert.AreEqual("Test content.", result.Content);
}
```

---

## Troubleshooting

| Problem | Likely cause | Solution |
|---------|--------------|----------|
| Tests fail to run | Incorrect .NET SDK version | Ensure .NET 9.0 SDK is installed and selected |
| In-memory database issues | Shared state between tests | Use unique database names or scoped contexts |
| Configuration problems | Missing or incorrect `appsettings.json` | Ensure correct configuration file is present |
| Dependency injection errors | Misconfigured services | Double-check service registrations and lifetimes |

Common solutions include repairing the solution, ensuring all dependencies are restored, and rebuilding the project.

---

Last updated: October 2025
