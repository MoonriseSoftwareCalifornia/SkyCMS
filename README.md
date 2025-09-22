# SkyCMS - High-Performance Content Management System

[![CodeQL](https://github.com/MoonriseSoftwareCalifornia/SkyCMS/actions/workflows/codeql.yml/badge.svg)](https://github.com/MoonriseSoftwareCalifornia/SkyCMS/actions/workflows/codeql.yml)
[![Publish Docker Images CI](https://github.com/MoonriseSoftwareCalifornia/SkyCMS/actions/workflows/docker-image.yml/badge.svg)](https://github.com/MoonriseSoftwareCalifornia/SkyCMS/actions/workflows/docker-image.yml)
[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](https://www.gnu.org/licenses/gpl-3.0)

[Project Website](https://Sky.moonrise.net) | [Documentation](https://Sky.moonrise.net/Docs) | [Get Free Help](https://Sky.moonrise.net/Support) | [YouTube Channel](https://www.youtube.com/@Sky-cms) | [Slack Channel](https://Sky-cms.slack.com/)

[![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FMoonriseSoftwareCalifornia%2FSkyCMS%2Frefs%2Fheads%2Fmain%2FArmTemplates%2Fazuredeploy.json)

[Read about what is deployed with this install.](./ArmTemplates/README.md)

## Overview

[SkyCMS](https://Sky.moonrise.net/) is an open-source, cloud-native Content Management System designed for high-performance, scalability, and ease of use. Built with modern web technologies, SkyCMS runs in multiple modes to meet different deployment needs:

- **Static Mode**: All content hosted on a static website with automatic content refresh - highest performance, stability, and operational simplicity
- **Headless Mode**: Content delivered via API - ideal for multi-channel content distribution (web, mobile, desktop)
- **Decoupled Mode**: Content delivered via dedicated website - near-static performance with backend functionality

## 🎯 Design Objectives

SkyCMS was built with the following core objectives:

- **Performance**: Outperform traditional CMSs in speed, capacity, and stability
- **User-Friendly**: Easy to use by both web developers and non-technical content editors
- **Low Maintenance**: Easy to administer with low operational costs
- **Flexible Deployment**: Support for static, decoupled, and headless modes
- **Cloud-Native**: Built for modern cloud infrastructure with global scalability

## 🚀 Use Cases

SkyCMS excels in demanding scenarios:

- **High-Capacity Websites**: Government sites during emergencies, news portals
- **Content-Heavy Platforms**: Media sites like New York Times, National Geographic, streaming platforms
- **Performance-Critical Applications**: Sites requiring minimal latency and efficient content delivery
- **Global Distribution**: Multi-regional redundancy with minimal administration overhead
- **Non-Technical Teams**: User-friendly interface requiring minimal training

## 🛠️ Content Editing Tools

SkyCMS integrates the best web content creation tools to provide a comprehensive editing experience:

### CKEditor 5

![CKEditor](ckeditor.webp)

Industry-standard WYSIWYG editor with rich text formatting, extensive plugin support, and intuitive interface. Perfect for non-technical users who want Word-like editing capabilities.

### GrapesJS

![GrapesJS](grapesjs.png)

Visual web builder with drag-and-drop interface for creating complex layouts without coding. Ideal for designing landing pages, newsletters, and custom templates.

[Watch our GrapesJS demo video](https://www.youtube.com/watch?v=mVGPlbnbC5c)

### Monaco Editor (Visual Studio Code)

![Monaco Editor](CodeEditor.png)

Powerful code editor for developers, featuring syntax highlighting, IntelliSense, and advanced editing capabilities. Includes diff tools and Emmet notation support.

### Filerobot Image Editor

![Filerobot](Filerobot.png)

Integrated image editing with resizing, cropping, filtering, and annotation capabilities. Edit images directly within the CMS without external tools.

### FilePond File Uploader

Modern file upload interface with drag-and-drop, image previews, and file validation. Supports multiple file types with progress tracking.

## 🏗️ Architecture & Technology Stack

### Core Applications

- **Editor Application** (`Editor/`): Content creation and management interface
- **Publisher Application** (`Publisher/`): Public-facing website renderer
- **Common Library** (`Common/`): Shared functionality and utilities
- **Blob Service** (`Cosmos.BlobService/`): File storage management
- **Dynamic Configuration** (`Cosmos.ConnectionStrings/`): Runtime configuration
- **Identity Framework** (`AspNetCore.Identity.FlexDb/`): User authentication and authorization

### Technology Stack

- **Backend**: ASP.NET Core 9.0+ (C#)
- **Frontend**: JavaScript (70% of codebase), HTML5, CSS3, SCSS
- **Database**: Azure Cosmos DB (NoSQL), MS SQL, MySQL or SQLite
- **Storage**: Azure Blob Storage or AWS S3 (and compatible)
- **Hosting**: Linux Docker containers
- **Authentication**: ASP.NET Core Identity, Google and Microsoft

### Infrastructure Components

- **Database Options**
  - Azure Cosmos DB: Multi-user, globally distributed NoSQL database
  - MS SQL, MySQL: Multi-user, globally distributed NoSQL database
  - SQLite: Built in database for single editor applications
- **Cloud Storage Options**
  - Azure Storage: File share and BLOB storage for web assets
  - Amazon S3 (and compatible): BLOB storage
  - Any SMB or NFS persistent file share storage

## 📁 Project Structure

```text
SkyCMS/
├── ArmTemplates/           # Azure Resource Manager deployment templates
├── Common/                 # Shared libraries and utilities
├── Cosmos.BlobService/     # File storage service layer
├── Cosmos.ConnectionStrings/ # Dynamic configuration management
├── AspNetCore.Identity.FlexDb/ # Flexible identity framework
├── Editor/                 # Content management application
├── Publisher/              # Public website application
├── docker-compose.yml      # Local development orchestration
└── SkyCMS.sln             # Visual Studio solution file
```

## � Component Documentation

Each component has detailed documentation explaining its purpose, configuration, and usage:

### Infrastructure & Deployment

- **[ARM Templates](./ArmTemplates/README.md)** - Azure deployment templates and infrastructure setup
  - Complete Azure Resource Manager templates
  - One-click deployment configuration
  - Email service integration (Azure Communication Services, SendGrid, SMTP)
  - Storage and database setup

### Applications

- **[Editor Application](./Editor/README.md)** - Content management interface
  - Article creation and editing with CKEditor 5, GrapesJS, and Monaco Editor
  - Media management with Filerobot image editor
  - User management and role-based access control
  - Real-time collaboration features

- **[Publisher Application](./Publisher/README.md)** - Public-facing website
  - High-performance content delivery
  - SEO optimization and sitemap generation
  - Multi-tenant support for hosting multiple websites
  - Static and dynamic content rendering

### Shared Libraries

- **[Common Library](./Common/README.md)** - Core shared functionality
  - Multi-database support (Cosmos DB, SQL Server, MySQL, SQLite)
  - Base controllers and data models
  - Authentication utilities and services
  - Article management and content processing

- **[Blob Service](./Cosmos.BlobService/README.md)** - Multi-cloud file storage
  - Azure Blob Storage and AWS S3 support
  - File management and media handling
  - CDN integration and performance optimization
  - Secure file access and permissions

- **[Dynamic Configuration](./Cosmos.ConnectionStrings/README.md)** - Runtime configuration management
  - Multi-tenant configuration support
  - Dynamic connection string management
  - Environment-specific settings
  - Configuration caching and performance

- **[Identity Framework](./AspNetCore.Identity.FlexDb/README.md)** - Flexible authentication
  - Multi-database identity provider support
  - ASP.NET Core Identity integration
  - Azure B2C and external provider support
  - Role-based security and permissions

## �🐳 Docker Containers

SkyCMS applications are distributed as Docker containers for consistent deployment:

- **Editor**: [`toiyabe/sky-editor:latest`](https://hub.docker.com/r/toiyabe/sky-editor)
- **Publisher**: [`toiyabe/sky-publisher:latest`](https://hub.docker.com/r/toiyabe/sky-publisher)
- **API**: [`toiyabe/sky-api:latest`](https://hub.docker.com/r/toiyabe/sky-api)

Alternative NodeJS Publisher: [Sky.Publisher.NodeJs](https://github.com/MoonriseSoftwareCalifornia/Sky.Publisher.NodeJs)

## 🚀 Quick Start

### Azure Deployment (Recommended)

1. Click the "Deploy to Azure" button above
2. Fill in required parameters (email configuration, storage options)
3. Deploy and access your SkyCMS instance

### Local Development

```bash
# Clone the repository
git clone https://github.com/MoonriseSoftwareCalifornia/SkyCMS.git
cd SkyCMS

# Run with Docker Compose
docker-compose up

# Or build and run locally
dotnet build SkyCMS.sln
dotnet run --project Editor
```

### System Requirements

- **.NET 9.0+** for local development
- **Docker** for containerized deployment
- **Azure/AWS/Google Cloud, etc...** for cloud deployment
- **Visual Studio 2022** or **VS Code** (recommended for development)

## 📖 Documentation

- **Installation Guide**: [sky.moonrise.net/install](/ArmTemplates/README.md)
- **Developer Documentation**: sky.moonrise.net/docs (coming soon)
- **API Reference**: Available in the running application
- **Video Tutorials**: YouTube Channel (coming soon)

## 🤝 Contributing

We welcome contributions! Please see our contributing guidelines and:

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable
5. Submit a pull request

## 📧 Email Configuration

SkyCMS supports multiple email providers:

- **Azure Communication Services**
- **SendGrid**
- **Any SMTP**

See the [deployment documentation](./ArmTemplates/README.md) for configuration details.

## 🌟 Key Features

- **Multi-Mode Operation**: Static, headless, and decoupled deployment options
- **High Performance**: Optimized for speed with blob storage and CDN integration
- **User-Friendly**: Intuitive interface for both developers and content creators
- **Scalable**: Built for high-traffic websites with global distribution
- **Secure**: Modern authentication with Azure B2C integration
- **Open Source**: GPL v3 licensed with active community support

<!-- ## 📞 Support

- **Free Community Support**: [sky.moonrise.net/support](https://sky.moonrise.net/support)
- **Slack Community**: [sky-cms.slack.com](https://sky-cms.slack.com/)
- **GitHub Issues**: Report bugs and request features
- **Professional Support**: Available through Moonrise Software -->

## 📄 License

This project is licensed under the GNU General Public License v3.0 - see the [LICENSE.md](LICENSE.md) file for details.

## 🙏 Acknowledgments

SkyCMS integrates several excellent open-source projects:

- CKEditor for rich text editing
- GrapesJS for visual page building
- Monaco Editor for code editing
- Filerobot for image editing
- FilePond for file uploads

---

**Copyright (c) 2025 Moonrise Software, LLC. All rights reserved.**

Built with ❤️ by the [Moonrise Software](https://moonrise.net) team.

