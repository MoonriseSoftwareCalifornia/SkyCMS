# JestTests — Frontend Test README

Purpose
- Contains JavaScript frontend tests and toolchain configuration (Jest).

Quick start
```powershell
cd JestTests
npm install
npm test
```

Where to look
- Test files under `JestTests/__tests__` or `JestTests/tests`.
- Frontend fixtures and mocks in the same folder.

Notes
- Use the local Node/NPM version recommended by the project (check `package.json`) and run tests after updating frontend assets.
# JestTests

JavaScript unit testing for SkyCMS using Jest.

## Setup

```powershell
# Navigate to JestTests directory
cd JestTests

# Install dependencies (first time only)
npm install
```

## Running Tests

```powershell
# Run all tests
npm test

# Run tests in watch mode (auto-rerun on changes)
npm run test:watch

# Run with coverage report
npm run test:coverage

# Run only API tests
npm run test:api

# Run only Editor tests
npm run test:editor

# Run only Editor unit suite explicitly
npm run test:unit:editor
```

## Test Structure

```
JestTests/
├── package.json           # Jest configuration & dependencies
├── tests/
│   ├── api/              # Contact API JavaScript tests
│   │   ├── skycms-contact.test.js
│   │   └── generated-script.test.js
│   └── editor/           # Editor JavaScript tests
│       ├── ckeditor-widget.test.js
│       ├── dublicator.test.js
│       ├── guid.test.js
│       └── image-widget.test.js
├── coverage/             # Coverage reports (generated)
└── node_modules/         # Dependencies (after npm install)
```

## What's Being Tested

### API Tests
- **skycms-contact.test.js**: Unit tests for the SkyCmsContact JavaScript library
  - Configuration validation
  - Form initialization
  - Field name mapping (default and custom)
  - Form submission handling
  - Success/error callbacks
  - CAPTCHA integration
  
- **generated-script.test.js**: Integration tests for the actual generated JavaScript from `/_api/contact/skycms-contact.js`

### Editor Tests
- **ckeditor-widget.test.js**: Config logic (GUID assignment for new regions, heading vs. content config selection)
- **dublicator.test.js**: Fresh `data-ccms-ceid` assignment for cloned blocks
- **image-widget.test.js**: GUID assignment for new image widgets
- **guid.test.js**: Shared GUID helper format/uniqueness

## Coverage Reports

After running `npm run test:coverage`, view the HTML report:

```powershell
# Open coverage report in browser
start coverage/lcov-report/index.html
```

## CI/CD Integration

Add to your build pipeline:

```yaml
- script: |
    cd JestTests
    npm install
    npm run test:coverage
  displayName: 'Run JavaScript Tests'
```
