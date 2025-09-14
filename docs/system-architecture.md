# SplitDuo System Architecture

```mermaid
graph TB
    %% User Layer
    User[👤 Users<br/>Couples/Partners]
    Mobile[📱 Mobile Browser<br/>Primary Interface]
    Desktop[🖥️ Desktop Browser<br/>Secondary Interface]

    User --> Mobile
    User --> Desktop

    %% Frontend Layer
    subgraph Frontend["🎨 Frontend Layer (sd-frontend)"]
        direction TB
        NuxtApp[Nuxt 4 Application]

        subgraph Pages["📄 Pages (File-based Routing)"]
            LoginPage[🔐 Login Page]
            Dashboard[📊 Dashboard]
            GroupsPage[👥 Groups Management]
            ExpensesPage[💰 Expenses Management]
            ProfilePage[👤 User Profile]
        end

        subgraph Components["🧩 Components"]
            AppHeader[📝 App Header]
            QuickActions[⚡ Quick Actions]
            FormComponents[📋 Form Components]
        end

        subgraph UIFramework["🎨 UI Framework"]
            NuxtUI[Nuxt UI Components]
            Tailwind[Tailwind CSS<br/>Mobile-First Design]
            Icons[Iconify<br/>Lucide + Simple Icons]
        end

        NuxtApp --> Pages
        NuxtApp --> Components
        NuxtApp --> UIFramework
    end

    Mobile --> Frontend
    Desktop --> Frontend

    %% API Gateway Layer
    subgraph APILayer["🌐 API Layer"]
        direction TB
        RestAPI[REST API<br/>v1 Endpoints]

        subgraph AuthAPI["🔐 Authentication"]
            LoginEndpoint[POST /auth/login]
            RefreshEndpoint[POST /auth/refresh]
            RevokeEndpoint[POST /auth/revoke]
        end

        subgraph CoreAPI["📊 Core Business APIs"]
            UsersAPI[👤 Users API<br/>/users]
            GroupsAPI[👥 Groups API<br/>/groups]
            ExpensesAPI[💰 Expenses API<br/>/expenses]
            SettlementsAPI[💳 Settlements API<br/>/settlements]
            BalancesAPI[⚖️ Balances API<br/>/balances]
        end

        subgraph DataAPI["📄 Data Management"]
            ImportAPI[📥 Import API<br/>/imports]
            ExportAPI[📤 Export API<br/>/export]
            CategoriesAPI[🏷️ Categories API<br/>/categories]
        end

        RestAPI --> AuthAPI
        RestAPI --> CoreAPI
        RestAPI --> DataAPI
    end

    Frontend --> APILayer

    %% Backend Layer
    subgraph Backend["⚙️ Backend Layer (sd-backend)"]
        direction TB

        subgraph APIProject["📁 SplitDuo.Api (.NET 9)"]
            direction TB
            Controllers[🎮 Controllers<br/>BaseApiController]

            subgraph VerticalSlices["📂 Features (Vertical Slices)"]
                AuthFeature[🔐 Authentication<br/>Login/Refresh/Revoke]
                UsersFeature[👤 Users<br/>CRUD Operations]
                GroupsFeature[👥 Groups<br/>Multi-user Management]
                ExpensesFeature[💰 Expenses<br/>Split Calculations]
                SettlementsFeature[💳 Settlements<br/>Payment Recording]
            end

            Middleware[🛡️ Middleware<br/>JWT Auth + Global Exception Handler]

            Controllers --> VerticalSlices
            Controllers --> Middleware
        end

        subgraph CoreProject["📁 SplitDuo.Core"]
            direction TB

            subgraph Services["🔧 Business Services"]
                AuthService[🔐 Authentication Service<br/>JWT + Refresh Tokens]
                UsersService[👤 Users Service<br/>Profile Management]
                GroupsService[👥 Groups Service<br/>Authorization + CRUD]
                ExpensesService[💰 Expenses Service<br/>Split Calculations]
                BalancesService[⚖️ Balances Service<br/>Debt Optimization]
                SettlementsService[💳 Settlements Service<br/>Payment Tracking]
            end

            subgraph Infrastructure["🏗️ Infrastructure Services"]
                EmailService[📧 SMTP Service<br/>MailKit]
                NotificationService[📬 Notification Service<br/>Outbox Pattern]
                UserContextService[👤 User Context Service<br/>JWT Claims]
            end

            subgraph DataAccess["📊 Data Access Layer"]
                UnitOfWork[📋 Unit of Work<br/>Transaction Management]
                AppDbContext[🗄️ App DB Context<br/>Entity Framework Core]
                Entities[📄 Domain Entities<br/>User, Group, Expense, etc.]
            end

            Services --> Infrastructure
            Services --> DataAccess
            Infrastructure --> DataAccess
            DataAccess --> Entities
        end

        APIProject --> CoreProject
    end

    APILayer --> Backend

    %% Database Layer
    subgraph Database["🗄️ Database Layer"]
        direction TB
        PostgreSQL[(PostgreSQL<br/>Primary Database)]

        subgraph Tables["📊 Database Tables"]
            UsersTable[👤 users<br/>Authentication & Profiles]
            GroupsTable[👥 groups<br/>Group Information]
            MembersTable[👨‍👩‍👧‍👦 group_members<br/>Membership & Roles]
            ExpensesTable[💰 expenses<br/>Expense Records]
            SplitsTable[🔄 expense_splits<br/>Split Calculations]
            SettlementsTable[💳 settlements<br/>Payment Records]
            TokensTable[🎫 refresh_tokens<br/>JWT Security]
            NotificationsTable[📬 notifications<br/>Email Queue]
            LogsTable[📋 logs<br/>Application Logs]
        end

        PostgreSQL --> Tables
    end

    Backend --> Database

    %% Background Services
    subgraph BackgroundJobs["⚙️ Background Services"]
        direction TB
        Quartz[🕐 Quartz.NET<br/>Job Scheduler]

        subgraph Jobs["⚡ Scheduled Jobs"]
            EmailJob[📧 Email Processing<br/>Queue Processing]
            EmailPrune[🧹 Email Cleanup<br/>30-day Retention]
            LogCleanup[📋 Log Cleanup<br/>30-day Retention]
        end

        Quartz --> Jobs
    end

    Backend --> BackgroundJobs

    %% External Services
    subgraph External["🌍 External Services"]
        direction TB
        SMTPServer[📧 SMTP Server<br/>Email Delivery]
        DockerRegistry[🐳 Docker Registry<br/>Container Images]
    end

    BackgroundJobs --> External

    %% Infrastructure Layer
    subgraph Infrastructure["🏗️ Infrastructure"]
        direction TB
        Docker[🐳 Docker Compose<br/>Container Orchestration]

        subgraph Containers["📦 Containers"]
            AppContainer[📦 App Container<br/>Frontend + Backend]
            DBContainer[🗄️ PostgreSQL Container<br/>Database]
        end

        subgraph Storage["💾 Storage"]
            DBVolume[💾 Database Volume<br/>Persistent Storage]
            LogVolume[📋 Log Volume<br/>Application Logs]
        end

        Docker --> Containers
        Containers --> Storage
    end

    Backend --> Infrastructure
    Database --> Infrastructure

    %% Data Flow Annotations
    classDef frontend fill:#e1f5fe,stroke:#0277bd,stroke-width:2px
    classDef backend fill:#e8f5e8,stroke:#2e7d32,stroke-width:2px
    classDef database fill:#fff3e0,stroke:#f57c00,stroke-width:2px
    classDef infrastructure fill:#fce4ec,stroke:#c2185b,stroke-width:2px
    classDef external fill:#f3e5f5,stroke:#7b1fa2,stroke-width:2px

    class Frontend,NuxtApp,Pages,Components,UIFramework frontend
    class Backend,APIProject,CoreProject,Services,Infrastructure,DataAccess backend
    class Database,PostgreSQL,Tables database
    class Infrastructure,Docker,Containers,Storage infrastructure
    class External,BackgroundJobs external
```

## Architecture Overview

**SplitDuo** is a modern expense splitting application built with a **microservice-inspired monolithic architecture** using containerization for deployment simplicity.

### Key Architectural Patterns

1. **Vertical Slice Architecture** - Backend organized by features rather than technical layers
2. **Mobile-First Design** - Frontend optimized for smartphone usage
3. **Single Container Deployment** - Frontend and backend served from one container
4. **Outbox Pattern** - Reliable email notifications with background processing
5. **Enhanced Result Pattern** - Type-safe error handling with HTTP status codes
6. **JWT with Refresh Tokens** - Secure authentication with token rotation

### Technology Stack

- **Frontend**: Nuxt 4, Vue 3, Nuxt UI, Tailwind CSS
- **Backend**: .NET 9, Entity Framework Core, ASP.NET Core Web API
- **Database**: PostgreSQL with comprehensive indexing
- **Authentication**: JWT Bearer tokens with refresh token rotation
- **Background Jobs**: Quartz.NET for email processing and cleanup
- **Email**: MailKit SMTP integration with outbox pattern
- **Deployment**: Docker Compose with multi-container setup
- **Logging**: Serilog with PostgreSQL sink and console output

### Core Features Implemented

✅ **User Management** - Secure user creation and profile management
✅ **Group Management** - Multi-user groups with role-based permissions
✅ **Expense Tracking** - Complete CRUD with automatic split calculations
✅ **Balance Calculations** - Real-time debt calculation with settlement optimization
✅ **Settlement Management** - Payment recording between group members
✅ **Authentication System** - JWT with secure refresh token rotation and 2FA support
✅ **Email Notifications** - Background email processing with retry logic
✅ **Data Persistence** - PostgreSQL with comprehensive entity relationships

### Security Features

- 🔐 **JWT Authentication** with 15-minute access tokens
- 🔄 **Refresh Token Rotation** for enhanced security
- 🛡️ **Group-based Authorization** with membership validation
- 📧 **Secure Email Processing** with outbox pattern
- 🔑 **Password Hashing** using ASP.NET Core Identity
- 🚫 **No Registration Endpoint** - admin-managed user creation only
- 🛡️ **Two-Factor Authentication** - TOTP, email codes, and backup codes
- 🔒 **Cryptographic Security** - SHA256 token hashing, secure random generation
- ⏱️ **Time-based Security** - Expiring verification codes with rate limiting

### Data Flow

1. **Authentication Flow**: Login → [2FA Verification] → JWT + Refresh Token → API Access → Token Refresh/Rotation
2. **Business Operations**: Frontend → REST API → Service Layer → Unit of Work → Database
3. **Email Processing**: Business Operations → Notification Queue → Background Job → SMTP Server
4. **Balance Calculations**: Expenses + Settlements → Balance Service → Optimization Algorithm → Settlement Suggestions

### Deployment Architecture

- **Single Application Container**: Contains both frontend (Nuxt) and backend (.NET)
- **Dedicated Database Container**: PostgreSQL with persistent volumes
- **Background Processing**: Integrated Quartz.NET scheduler within application
- **Email Integration**: SMTP configuration for notification delivery
- **Log Management**: Centralized logging with 30-day retention policy

This architecture supports the initial **two-person expense splitting** use case while maintaining extensibility for future **multi-user group expansion** and additional features.
