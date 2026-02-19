# Contact Form API Documentation

## Overview

The Contact Form API provides a complete, production-ready solution for handling visitor messages on your website. It includes:

- Form submission and processing
- Email notifications to administrators
- CAPTCHA protection (optional)
- Rate limiting per IP address
- Anti-forgery token protection
- Comprehensive error handling

## Architecture

### Deployment Pattern

The Contact Form API is typically deployed behind a **CloudFlare Worker** that acts as a reverse proxy. This architecture provides several benefits:

```
┌─────────────────────────┐
│  Public Website Domain  │
│  (visitor.example.com)  │
└────────────┬────────────┘
             │ /_api/contact/*
             ↓
     ┌──────────────────┐
     │ CloudFlare Worker│ (Proxy/Gateway)
     │ • Route requests │
     │ • Validate CORS  │
     │ • Set headers    │
     └────────┬─────────┘
              │ x-origin-hostname header
              ↓
    ┌──────────────────────────┐
    │ SkyCMS Editor API        │
    │ (edit.example.com)       │
    │ • Process submissions    │
    │ • Send notifications     │
    │ • Validate CAPTCHA       │
    └──────────────────────────┘
```

### Why Use a CloudFlare Worker?

1. **Tenant Resolution** - The worker sets the `x-origin-hostname` header, enabling SkyCMS's multi-tenant system to route requests to the correct tenant
2. **CORS Handling** - Allows cross-origin requests from your public domain to the backend API
3. **Request Routing** - Intelligently routes `/_api/contact/*` requests to the backend
4. **Security** - Restricts access to your zone domain and validates request methods
5. **Global Distribution** - CloudFlare's CDN ensures low latency worldwide
6. **Error Handling** - Gracefully handles backend failures with meaningful error responses

### Worker Configuration

The CloudFlare Worker is configured with:

- **Routes**: Matches `/_api/contact/*` requests on your domain
- **Method Restrictions**: Only allows GET (for form scripts) and POST (for submissions)
- **CORS Validation**: Restricts requests to your configured zone domain
- **Header Management**: 
  - Sets `host` to backend hostname
  - Sets `x-origin-hostname` for multi-tenant routing
  - Forwards all other headers to backend
- **Error Handling**: Returns 503 if backend is unavailable

For detailed worker configuration and deployment instructions, see [CloudFlare Worker README](https://github.com/CWALabs/SkyCMS/tree/main/Scripts/CloudFlare/Workers/ContactUsApi).

## Endpoints

### 1. Get Contact Script

Returns a JavaScript library that handles client-side CAPTCHA setup and form submission.

**Endpoint**: `GET /_api/contact/skycms-contact.js`

**Authentication**: None required

**Response**:
- **Status**: 200 OK
- **Content-Type**: `application/javascript`
- **Body**: JavaScript code with embedded CAPTCHA configuration

**Example Request**:
```javascript
// Load the script in your HTML
<script src="/_api/contact/skycms-contact.js"></script>
```

**What it provides**:
- Embedded anti-forgery token for form submission
- CAPTCHA configuration (provider type, site key)
- Complete JavaScript library with `SkyCmsContact` object
- Auto-loading of CAPTCHA scripts (Turnstile or reCAPTCHA)
- Form handling and submission utilities

## JavaScript Library Reference

The endpoint returns a complete JavaScript library that exposes a global `SkyCmsContact` object. This section documents the API.

### Global Object: `SkyCmsContact`

The returned script creates a `SkyCmsContact` object on the window scope with the following interface:

#### Configuration Properties

```javascript
SkyCmsContact.config = {
  requireCaptcha: boolean,           // Whether CAPTCHA is required
  captchaProvider: string,           // "turnstile", "recaptcha", or null
  captchaSiteKey: string,            // Public CAPTCHA key
  antiforgeryToken: string,          // Anti-forgery token
  submitEndpoint: string,            // Default: "/_api/contact/submit"
  maxMessageLength: number,          // Default: 5000
  fieldNames: {                      // Field name mapping
    name: string,                    // Default: "name"
    email: string,                   // Default: "email"
    message: string                  // Default: "message"
  }
}
```

#### Methods

##### `init(formSelector, options)`

Initializes the contact form with automatic submission handling.

**Parameters**:
- `formSelector` (string | HTMLFormElement): CSS selector or HTML element reference for the form
- `options` (Object, optional): Configuration overrides
  - `fieldNames` (Object, optional): Custom field name mapping
    - `name` (string): Name of the name input field (default: `'name'`)
    - `email` (string): Name of the email input field (default: `'email'`)
    - `message` (string): Name of the message input field (default: `'message'`)
  - `onSuccess` (Function): Callback when submission succeeds
  - `onError` (Function): Callback when submission fails
  - Other config properties to override defaults

**Returns**: undefined

**Example**:
```javascript
// Initialize with default field names
SkyCmsContact.init('#contactForm');

// Initialize with custom field names
SkyCmsContact.init('#contactForm', {
  fieldNames: {
    name: 'fullName',
    email: 'emailAddress',
    message: 'userMessage'
  }
});

// With custom field names AND callbacks
SkyCmsContact.init('#contactForm', {
  fieldNames: {
    name: 'customerName',
    email: 'customerEmail',
    message: 'comments'
  },
  onSuccess: (result) => {
    console.log('Success:', result.message);
    document.getElementById('successMessage').style.display = 'block';
  },
  onError: (result) => {
    console.error('Error:', result.message);
    document.getElementById('errorMessage').textContent = result.message;
  }
});

// Or with element reference
const form = document.getElementById('contactForm');
SkyCmsContact.init(form, {
  onSuccess: (result) => alert(result.message)
});
```

**Expected Form Structure (Default Field Names)**:
The form should contain input fields with these **exact names** (unless overridden with `fieldNames` option):
- `name` - Sender's name
- `email` - Sender's email
- `message` - Message content

```html
<form id="contactForm">
  <input type="text" name="name" required>
  <input type="email" name="email" required>
  <textarea name="message" required></textarea>
  <button type="submit">Send</button>
</form>
```

**Using Custom Field Names**:
If your form uses different field names, simply specify them in the options:

```html
<form id="contactForm">
  <input type="text" name="fullName" required>
  <input type="email" name="emailAddress" required>
  <textarea name="userMessage" required></textarea>
  <button type="submit">Send</button>
</form>

<script src="/_api/contact/skycms-contact.js"></script>
<script>
  SkyCmsContact.init('#contactForm', {
    fieldNames: {
      name: 'fullName',
      email: 'emailAddress',
      message: 'userMessage'
    }
  });
</script>
```

##### `handleSubmit(form, config)`

Handles form submission (called automatically by `init()`). Can also be called manually.

**Parameters**:
- `form` (HTMLFormElement): The form to submit
- `config` (Object): Configuration object

**Returns**: Promise<void>

**Behavior**:
1. Extracts form data (name, email, message)
2. Obtains CAPTCHA token if required
3. Sends POST request to `submitEndpoint`
4. Calls `onSuccess` or `onError` callback with result

**Example**:
```javascript
const form = document.getElementById('contactForm');
SkyCmsContact.handleSubmit(form, SkyCmsContact.config)
  .catch(error => console.error('Submission error:', error));
```

##### `loadCaptcha(config)`

Loads the CAPTCHA library script (called automatically by `init()`).

**Parameters**:
- `config` (Object): Configuration object with `captchaProvider` and `captchaSiteKey`

**Returns**: undefined

**Behavior**:
- For Turnstile: Loads `https://challenges.cloudflare.com/turnstile/v0/api.js`
- For reCAPTCHA: Loads `https://www.google.com/recaptcha/api.js?render={siteKey}`
- For none: Does nothing

**Example**:
```javascript
SkyCmsContact.loadCaptcha(SkyCmsContact.config);
```

##### `getCaptchaToken(config)`

Obtains a CAPTCHA validation token from the configured provider.

**Parameters**:
- `config` (Object): Configuration object with `captchaProvider` and `captchaSiteKey`

**Returns**: Promise<string> - CAPTCHA token

**Behavior**:
- **Turnstile**: Renders widget and waits for user validation
- **reCAPTCHA**: Executes reCAPTCHA v3 (invisible)
- **None**: Returns empty string

**Throws**:
- Error if CAPTCHA validation fails or times out (after 10 seconds)

**Example**:
```javascript
try {
  const token = await SkyCmsContact.getCaptchaToken(SkyCmsContact.config);
  console.log('Got CAPTCHA token:', token);
} catch (error) {
  console.error('CAPTCHA error:', error.message);
}
```

### Complete Usage Examples

#### Simple Initialization (Default Field Names)

```html
<!DOCTYPE html>
<html>
<head>
  <title>Contact Form</title>
</head>
<body>
  <form id="contactForm">
    <div>
      <label for="name">Name:</label>
      <input type="text" id="name" name="name" required minlength="2" maxlength="100">
    </div>
    
    <div>
      <label for="email">Email:</label>
      <input type="email" id="email" name="email" required maxlength="255">
    </div>
    
    <div>
      <label for="message">Message:</label>
      <textarea id="message" name="message" required minlength="10" maxlength="5000"></textarea>
    </div>
    
    <button type="submit">Send Message</button>
  </form>

  <!-- Load the SkyCMS contact library -->
  <script src="/_api/contact/skycms-contact.js"></script>

  <script>
    // Initialize with default field names
    SkyCmsContact.init('#contactForm');
  </script>
</body>
</html>
```

#### Using Custom Field Names

```html
<!DOCTYPE html>
<html>
<head>
  <title>Contact Form</title>
</head>
<body>
  <form id="contactForm">
    <div>
      <label for="fullName">Full Name:</label>
      <input type="text" id="fullName" name="fullName" required>
    </div>
    
    <div>
      <label for="emailAddress">Email Address:</label>
      <input type="email" id="emailAddress" name="emailAddress" required>
    </div>
    
    <div>
      <label for="userMessage">Your Message:</label>
      <textarea id="userMessage" name="userMessage" required></textarea>
    </div>
    
    <button type="submit">Submit</button>
  </form>

  <script src="/_api/contact/skycms-contact.js"></script>

  <script>
    // Specify custom field names
    SkyCmsContact.init('#contactForm', {
      fieldNames: {
        name: 'fullName',
        email: 'emailAddress',
        message: 'userMessage'
      }
    });
  </script>
</body>
</html>
```

#### Advanced Initialization with Callbacks

```html
<script src="/_api/contact/skycms-contact.js"></script>

<script>
  SkyCmsContact.init('#contactForm', {
    onSuccess: (result) => {
      console.log('Form submitted successfully');
      console.log('Message:', result.message);
      
      // Show success message
      const successDiv = document.createElement('div');
      successDiv.className = 'success-message';
      successDiv.textContent = result.message;
      document.body.appendChild(successDiv);
      
      // Reset form
      document.getElementById('contactForm').reset();
      
      // Auto-hide message after 5 seconds
      setTimeout(() => successDiv.remove(), 5000);
    },
    
    onError: (result) => {
      console.error('Form submission failed');
      console.error('Error:', result.error);
      console.error('Message:', result.message);
      
      // Show error message
      const errorDiv = document.createElement('div');
      errorDiv.className = 'error-message';
      errorDiv.innerHTML = `<strong>Error:</strong> ${result.message}`;
      document.body.appendChild(errorDiv);
      
      // Auto-hide message after 10 seconds
      setTimeout(() => errorDiv.remove(), 10000);
    }
  });
</script>
```

#### Manual Form Handling

```javascript
<script src="/_api/contact/skycms-contact.js"></script>

<script>
  const form = document.getElementById('contactForm');
  
  // Manual submit handling (not using init())
  form.addEventListener('submit', async (e) => {
    e.preventDefault();
    
    console.log('Submitting form...');
    
    try {
      // Get form data
      const formData = new FormData(form);
      const data = {
        name: formData.get('name'),
        email: formData.get('email'),
        message: formData.get('message')
      };
      
      // Get CAPTCHA token if required
      if (SkyCmsContact.config.requireCaptcha) {
        console.log('Getting CAPTCHA token...');
        data.captchaToken = await SkyCmsContact.getCaptchaToken(SkyCmsContact.config);
      }
      
      // Submit
      const response = await fetch(SkyCmsContact.config.submitEndpoint, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'RequestVerificationToken': SkyCmsContact.config.antiforgeryToken
        },
        body: JSON.stringify(data)
      });
      
      const result = await response.json();
      
      if (result.success) {
        console.log('Success!', result.message);
        form.reset();
      } else {
        console.error('Failed:', result.error);
      }
    } catch (error) {
      console.error('Submission error:', error);
    }
  });
</script>
```

#### With Custom Form Validation

```javascript
<script src="/_api/contact/skycms-contact.js"></script>

<script>
  SkyCmsContact.init('#contactForm', {
    onSuccess: (result) => {
      // Show success
      alert(result.message);
      document.getElementById('contactForm').reset();
    },
    onError: (result) => {
      // Show custom error UI
      const feedback = document.getElementById('feedback');
      feedback.className = 'error';
      feedback.textContent = result.message;
      feedback.style.display = 'block';
    }
  });
  
  // Add real-time validation feedback
  const nameInput = document.querySelector('input[name="name"]');
  const emailInput = document.querySelector('input[name="email"]');
  const messageInput = document.querySelector('textarea[name="message"]');
  
  nameInput.addEventListener('blur', function() {
    if (this.value.length < 2 || this.value.length > 100) {
      this.classList.add('error');
    } else {
      this.classList.remove('error');
    }
  });
  
  emailInput.addEventListener('blur', function() {
    const isValid = /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(this.value);
    if (!isValid) {
      this.classList.add('error');
    } else {
      this.classList.remove('error');
    }
  });
  
  messageInput.addEventListener('input', function() {
    const count = this.value.length;
    const counter = document.getElementById('messageCounter');
    if (counter) {
      counter.textContent = `${count}/${SkyCmsContact.config.maxMessageLength}`;
    }
  });
</script>
```

### Configuration Embed Details

The script automatically embeds:

1. **Anti-forgery Token**: Generated server-side and included in the script
2. **CAPTCHA Configuration**:
   - Whether CAPTCHA is required
   - Provider type (turnstile, recaptcha, or none)
   - Site key (public key for client-side CAPTCHA)
3. **Timestamps**: UTC timestamp of script generation for debugging
4. **Submit Endpoint**: Default is `/_api/contact/submit`
5. **Max Message Length**: Server-configured limit

### Error Handling in JavaScript

Common errors and how to handle them:

```javascript
SkyCmsContact.init('#contactForm', {
  onError: (result) => {
    const errorType = result.error;
    
    switch (errorType) {
      case 'Missing CAPTCHA token':
        console.error('CAPTCHA validation was required but token is missing');
        break;
      case 'Invalid CAPTCHA':
        console.error('CAPTCHA token validation failed');
        break;
      case 'Email delivery failed':
        console.error('Email could not be sent. Try again later.');
        break;
      case 'Rate limit exceeded':
        console.error('Too many requests. Please wait before trying again.');
        break;
      default:
        console.error('Unknown error:', result.error);
    }
    
    // Display user-friendly message
    alert(result.message);
  }
});
```

### Browser Compatibility

- Requires ES6+ JavaScript support (async/await)
- Works on all modern browsers (Chrome, Firefox, Safari, Edge)
- Requires that the form is present in DOM before initialization
- Requires HTTPS for CAPTCHA providers

### Custom Field Names

The library now supports custom field names natively through the `fieldNames` configuration option. Simply pass your field names when initializing:

```javascript
SkyCmsContact.init('#contactForm', {
  fieldNames: {
    name: 'yourNameField',
    email: 'yourEmailField',
    message: 'yourMessageField'
  }
});
```

#### Complete Example with Custom Fields

```html
<form id="customContactForm">
  <!-- Your custom field names -->
  <input type="text" name="customerName" required>
  <input type="email" name="customerEmail" required>
  <textarea name="customerComments" required></textarea>
  <button type="submit">Send</button>
</form>

<script src="/_api/contact/skycms-contact.js"></script>
<script>
  SkyCmsContact.init('#customContactForm', {
    fieldNames: {
      name: 'customerName',
      email: 'customerEmail',
      message: 'customerComments'
    },
    onSuccess: (result) => {
      alert(result.message);
      document.getElementById('customContactForm').reset();
    },
    onError: (result) => {
      alert('Error: ' + result.message);
    }
  });
</script>
```

#### Partial Override

You can override just one or two field names while keeping the defaults for others:

```javascript
// Only override the message field name
SkyCmsContact.init('#contactForm', {
  fieldNames: {
    message: 'comments'  // name and email still use defaults
  }
});
```

#### Legacy Support

If you prefer to handle field mapping manually (for advanced scenarios), you can still use the lower-level `handleSubmit()` method. See the [JavaScript Library Reference](#javascript-library-reference) for details.

### Performance Notes

- Script size: ~3-5 KB (varies with CAPTCHA provider configuration)
- CAPTCHA loading: Lazy-loaded only when needed
- Turnstile loading time: ~500ms
- reCAPTCHA loading time: ~300ms
- Form submission: < 100ms (excluding CAPTCHA validation and network latency)

### 2. Submit Contact Form

Submits a contact form message.

**Endpoint**: `POST /_api/contact/submit`

**Authentication**: Anti-forgery token required

**Rate Limit**: 5 requests per minute per IP address

**Request Header**:
```
Content-Type: application/json
```

**Request Body**:
```json
{
  "name": "John Doe",
  "email": "john@example.com",
  "message": "I have a question about your services...",
  "captchaToken": "optional-captcha-token"
}
```

**Request Field Descriptions**:

| Field | Type | Required | Validation | Description |
|-------|------|----------|-----------|-------------|
| name | string | Yes | 2-100 characters | Sender's name |
| email | string | Yes | Valid email format | Sender's email address |
| message | string | Yes | 10-5000 characters | Contact message content |
| captchaToken | string | Conditional | Required if `RequireCaptcha` is true | CAPTCHA verification token |

**Response - Success (200 OK)**:
```json
{
  "success": true,
  "message": "Thank you for your message. We'll get back to you soon!",
  "error": null
}
```

**Response - Validation Error (400 Bad Request)**:
```json
{
  "success": false,
  "message": "Validation failed",
  "error": "Message must be between 10 and 5000 characters"
}
```

**Response - CAPTCHA Failed (400 Bad Request)**:
```json
{
  "success": false,
  "message": "We're sorry, but there was a problem with your submission.",
  "error": "CAPTCHA validation failed"
}
```

**Response - Rate Limited (429 Too Many Requests)**:
```json
{
  "success": false,
  "message": "Too many requests. Please try again later.",
  "error": "Rate limit exceeded"
}
```

**Response - Email Delivery Failed (400 Bad Request)**:
```json
{
  "success": false,
  "message": "We're sorry, but there was a problem sending your message. Please try again later.",
  "error": "Email delivery failed"
}
```

**Response - Server Error (500 Internal Server Error)**:
```json
{
  "success": false,
  "message": "An unexpected error occurred. Please try again later.",
  "error": "Exception message details (only in development)"
}
```

## Usage Examples

### HTML Form

The simplest way to use the Contact Form API is to load the generated JavaScript library and initialize it:

```html
<!DOCTYPE html>
<html>
<head>
  <title>Contact Us</title>
</head>
<body>
  <h1>Contact Us</h1>
  
  <form id="contactForm">
    <div>
      <label for="name">Name:</label>
      <input type="text" id="name" name="name" required minlength="2" maxlength="100">
    </div>
    
    <div>
      <label for="email">Email:</label>
      <input type="email" id="email" name="email" required maxlength="255">
    </div>
    
    <div>
      <label for="message">Message:</label>
      <textarea id="message" name="message" required minlength="10" maxlength="5000"></textarea>
    </div>
    
    <button type="submit">Submit</button>
  </form>

  <!-- Load the SkyCMS Contact Form library -->
  <script src="/_api/contact/skycms-contact.js"></script>
  
  <!-- Initialize the form -->
  <script>
    // Simple initialization with default behavior
    SkyCmsContact.init('#contactForm');
    
    // Or with custom callbacks
    SkyCmsContact.init('#contactForm', {
      onSuccess: (result) => {
        alert(result.message);
      },
      onError: (result) => {
        alert(`Error: ${result.error}`);
      }
    });
  </script>
</body>
</html>
```

The library handles:
- Form data extraction (supports custom field names via `fieldNames` option)
- CAPTCHA validation (if configured)
- Anti-forgery token inclusion
- Error handling
- Success/error callbacks

### cURL

**Get Script**:
```bash
curl -X GET "https://yourdomain.com/_api/contact/skycms-contact.js" \
  -H "Accept: application/javascript"
```

**Submit Form**:
```bash
curl -X POST "https://yourdomain.com/_api/contact/submit" \
  -H "Content-Type: application/json" \
  -H "X-CSRF-TOKEN: your-token-here" \
  -d '{
    "name": "John Doe",
    "email": "john@example.com",
    "message": "I would like to know more about your services.",
    "captchaToken": "token-from-captcha-provider"
  }'
```

### JavaScript/Fetch

```javascript
async function submitContactForm(formData, captchaToken) {
  try {
    const response = await fetch('/_api/contact/submit', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({
        name: formData.name,
        email: formData.email,
        message: formData.message,
        captchaToken: captchaToken
      })
    });

    const result = await response.json();
    
    if (!response.ok) {
      console.error('Submission failed:', result.error);
      return { success: false, message: result.message };
    }
    
    console.log('Submission successful:', result.message);
    return { success: true, message: result.message };
  } catch (error) {
    console.error('Request failed:', error);
    return { success: false, message: 'Network error occurred' };
  }
}

// Usage
submitContactForm(
  {
    name: 'Jane Smith',
    email: 'jane@example.com',
    message: 'I have a question about pricing.'
  },
  'captcha-token-here'
).then(result => {
  if (result.success) {
    alert(result.message);
  }
});
```

## CAPTCHA Integration

### Turnstile (Cloudflare)

The API supports Cloudflare Turnstile for bot protection.

**Setup**:
1. Create a Turnstile account at https://dash.cloudflare.com/
2. Add a Turnstile site
3. Configure in your application:
   ```json
   {
     "ContactApi": {
       "RequireCaptcha": true,
       "CaptchaProvider": "turnstile",
       "CaptchaSiteKey": "your-site-key",
       "CaptchaSecretKey": "your-secret-key"
     }
   }
   ```

**Client-Side Implementation**:
```html
<script src="https://challenges.cloudflare.com/turnstile/v0/api.js" async defer></script>

<form id="contactForm">
  <!-- Form fields -->
  <div class="cf-turnstile" data-sitekey="YOUR_SITE_KEY"></div>
  <button type="submit">Submit</button>
</form>

<script>
  document.getElementById('contactForm').addEventListener('submit', async (e) => {
    e.preventDefault();
    
    const token = document.querySelector('[name=cf-turnstile-response]').value;
    
    // Submit with token
    const response = await fetch('/_api/contact/submit', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        name: document.getElementById('name').value,
        email: document.getElementById('email').value,
        message: document.getElementById('message').value,
        captchaToken: token
      })
    });
  });
</script>
```

### reCAPTCHA (Google)

The API supports Google reCAPTCHA v3 for invisible bot protection.

**Setup**:
1. Register at https://www.google.com/recaptcha/admin
2. Create a reCAPTCHA v3 site
3. Configure in your application:
   ```json
   {
     "ContactApi": {
       "RequireCaptcha": true,
       "CaptchaProvider": "recaptcha",
       "CaptchaSiteKey": "your-site-key",
       "CaptchaSecretKey": "your-secret-key"
     }
   }
   ```

**Client-Side Implementation**:
```html
<script src="https://www.google.com/recaptcha/api.js"></script>

<form id="contactForm">
  <!-- Form fields -->
  <input type="hidden" id="recaptchaToken" name="captchaToken">
  <button type="submit">Submit</button>
</form>

<script>
  grecaptcha.ready(() => {
    document.getElementById('contactForm').addEventListener('submit', async (e) => {
      e.preventDefault();
      
      const token = await grecaptcha.execute('YOUR_SITE_KEY', { action: 'contact' });
      document.getElementById('recaptchaToken').value = token;
      
      // Submit form with token
      const response = await fetch('/_api/contact/submit', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          name: document.getElementById('name').value,
          email: document.getElementById('email').value,
          message: document.getElementById('message').value,
          captchaToken: token
        })
      });
    });
  });
</script>
```

## Error Handling

The API returns meaningful error messages for common issues:

| Error | Status Code | Cause | Solution |
|-------|-------------|-------|----------|
| "Name is required" | 400 | Name field is empty | Provide a name |
| "Name must be between 2 and 100 characters" | 400 | Name is too short/long | Adjust name length |
| "Invalid email address" | 400 | Email format is invalid | Use a valid email |
| "Message must be between 10 and 5000 characters" | 400 | Message is too short/long | Adjust message length |
| "CAPTCHA validation failed" | 400 | CAPTCHA check failed | Verify CAPTCHA token is valid |
| "Rate limit exceeded" | 429 | Too many requests from IP | Wait before retrying |
| "Email delivery failed" | 400 | Email service failure | Retry later |
| "An unexpected error occurred" | 500 | Server-side error | Check logs and retry |

## Logging

All submissions are logged with the following information:
- Submitter's email address
- Submitter's IP address
- Submission timestamp
- Success/failure status
- Error details (if applicable)

This can be used for:
- Audit trails
- Abuse detection and prevention
- Debugging issues
- Monitoring service health

## Security Considerations

1. **CSRF Protection**: All POST requests require a valid anti-forgery token
2. **Rate Limiting**: 5 requests per minute per IP prevents spam/abuse
3. **Input Validation**: All fields are validated with strict length limits
4. **Email Validation**: RFC 5322 compliant validation prevents invalid emails
5. **CAPTCHA**: Optional but recommended for production deployments
6. **Secret Keys**: CAPTCHA secret keys should be stored securely and never exposed

## Performance

- Average response time: < 100ms (without CAPTCHA)
- Email sending: Asynchronous (doesn't block response)
- CAPTCHA validation: ≤ 2 seconds (depends on provider)
- Rate limiting: < 1ms overhead

## Troubleshooting

### "Anti-forgery token is missing"
- Ensure you're calling the script endpoint first to get the token
- Check that the token is being sent in the request header or body

### "CAPTCHA token is invalid"
- Verify the token is fresh (some providers have expiration)
- Ensure the token matches the configured provider
- Check that site/secret keys are correct

### Submissions not arriving
- Verify `AdminEmail` is configured correctly
- Check email service logs in the host application
- Ensure email isn't being filtered as spam

### Rate limiting too strict
- This is configured per IP address
- If requests are being blocked, wait 1 minute before retrying
- Contact your administrator to adjust limits if needed

## Future Enhancements

Potential future features:
- File attachment support
- Custom field support
- Webhook notifications
- Database persistence of submissions
- Admin dashboard for managing submissions
